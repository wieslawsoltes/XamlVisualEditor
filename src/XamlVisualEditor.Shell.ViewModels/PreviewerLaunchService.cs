using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using XamlVisualEditor.Core;

namespace XamlVisualEditor.Shell.ViewModels;

internal sealed class PreviewerLaunchService : IDisposable
{
    private readonly Dictionary<string, Process> _processes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PreviewerTcpSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly PreviewerTelemetryStore _telemetry = new();

    public event Action<PreviewerErrorInfo>? PreviewerErrorReceived;

    public async Task<PreviewerLaunchResult> StartPreviewerAsync(
        string xamlFilePath,
        string? xamlText,
        WorkspaceModel workspace,
        string? workspacePath,
        Func<string, Task>? runWorkspaceCommandAsync,
        Action<string, string>? log)
    {
        if (!TryCreateLaunchInfo(xamlFilePath, workspace, out PreviewerLaunchInfo launchInfo, out string error))
        {
            return PreviewerLaunchResult.Fail(error);
        }

        if (!launchInfo.HasRequiredAssets)
        {
            if (runWorkspaceCommandAsync is not null && !string.IsNullOrWhiteSpace(workspacePath))
            {
                log?.Invoke("Info", "Previewer assets missing. Building workspace...");
                await runWorkspaceCommandAsync("build");
            }

            if (!TryCreateLaunchInfo(xamlFilePath, workspace, out launchInfo, out error))
            {
                return PreviewerLaunchResult.Fail(error);
            }
        }

        if (_processes.TryGetValue(xamlFilePath, out Process? existing) && !existing.HasExited)
        {
            if (!string.IsNullOrWhiteSpace(xamlText))
            {
                await SendUpdateXamlAsync(xamlFilePath, xamlText, workspace, log);
            }

            return PreviewerLaunchResult.FromProcess(existing);
        }

        PreviewerTcpSession session = GetOrCreateSession(xamlFilePath, log);
        launchInfo = launchInfo with { TransportUri = $"tcp-bson://127.0.0.1:{session.Port}/" };
        LogCompiledBindingsSetting(launchInfo.TargetAssemblyPath, log);
        ProcessStartInfo startInfo = BuildStartInfo(launchInfo);
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Exited += (_, _) =>
            {
                _processes.Remove(xamlFilePath);
                if (_sessions.Remove(xamlFilePath, out PreviewerTcpSession? removed))
                {
                    removed.Dispose();
                }

                if (process.ExitCode != 0)
                {
                    _telemetry.RecordCrash(process.ExitCode);
                }

                log?.Invoke("Info", $"Previewer exited with code {process.ExitCode}");
            };
            process.Start();
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    log?.Invoke("Info", $"Previewer: {e.Data}");
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    log?.Invoke("Error", $"Previewer: {e.Data}");
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            stopwatch.Stop();
            _telemetry.RecordStart(stopwatch.Elapsed);
            _processes[xamlFilePath] = process;
            log?.Invoke("Info", $"Previewer started: {launchInfo.TargetAssemblyPath}");
            if (!string.IsNullOrWhiteSpace(xamlText))
            {
                await SendUpdateXamlAsync(xamlFilePath, xamlText, workspace, log);
            }
            return PreviewerLaunchResult.FromProcess(process);
        }
        catch (Exception ex)
        {
            _telemetry.RecordFailure(ex.Message);
            return PreviewerLaunchResult.Fail($"Previewer launch failed: {ex.Message}");
        }
    }

    public async Task SendUpdateXamlAsync(
        string xamlFilePath,
        string xamlText,
        WorkspaceModel workspace,
        Action<string, string>? log)
    {
        if (!_sessions.TryGetValue(xamlFilePath, out PreviewerTcpSession? session))
        {
            return;
        }

        if (!TryCreateLaunchInfo(xamlFilePath, workspace, out PreviewerLaunchInfo launchInfo, out _))
        {
            return;
        }

        string projectPath = BuildXamlProjectPath(xamlFilePath, launchInfo.ProjectDirectory);
        string updatedXaml = NormalizeDesignDataContext(xamlText);
        (double? width, double? height) = TryGetDesignSize(updatedXaml);
        await session.SendUpdateXamlAsync(updatedXaml, launchInfo.TargetAssemblyPath, projectPath, width, height);
        log?.Invoke("Info", "Previewer XAML update sent");
    }

    public bool TryGetSession(string xamlFilePath, out PreviewerTcpSession? session)
        => _sessions.TryGetValue(xamlFilePath, out session);

    public void Dispose()
    {
        foreach (Process process in _processes.Values)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }

        _processes.Clear();

        foreach (PreviewerTcpSession session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
    }

    private static ProcessStartInfo BuildStartInfo(PreviewerLaunchInfo launchInfo)
    {
        (string runtimeConfigPath, string depsFilePath) = ResolveHostRuntimeFiles(launchInfo);
        string args = string.Join(" ", new[]
        {
            "exec",
            $"--runtimeconfig \"{runtimeConfigPath}\"",
            $"--depsfile \"{depsFilePath}\"",
            $"\"{launchInfo.HostPath}\"",
            $"--transport {launchInfo.TransportUri}",
            launchInfo.MethodArguments,
            $"\"{launchInfo.TargetAssemblyPath}\""
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = args,
            WorkingDirectory = launchInfo.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static (string RuntimeConfigPath, string DepsFilePath) ResolveHostRuntimeFiles(PreviewerLaunchInfo launchInfo)
    {
        string hostRuntimeConfig = Path.ChangeExtension(launchInfo.HostPath, ".runtimeconfig.json");
        string hostDepsFile = Path.ChangeExtension(launchInfo.HostPath, ".deps.json");

        if (File.Exists(hostRuntimeConfig) && File.Exists(hostDepsFile))
        {
            return (hostRuntimeConfig, hostDepsFile);
        }

        return (launchInfo.RuntimeConfigPath, launchInfo.DepsFilePath);
    }

    private static bool TryCreateLaunchInfo(
        string xamlFilePath,
        WorkspaceModel workspace,
        out PreviewerLaunchInfo launchInfo,
        out string error)
    {
        launchInfo = default!;
        error = string.Empty;

        ProjectModel? project = FindProjectForFile(workspace, xamlFilePath);
        if (project is null)
        {
            error = "No project found for XAML file.";
            return false;
        }

        string? projectDir = Path.GetDirectoryName(project.ProjectPath);

        string? targetAssemblyPath = ResolveTargetAssemblyPath(project);
        if (string.IsNullOrWhiteSpace(targetAssemblyPath) || !File.Exists(targetAssemblyPath))
        {
            error = "Project output assembly not found. Build the project first.";
            return false;
        }

        string? outputDir = Path.GetDirectoryName(targetAssemblyPath);
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            error = "Project output directory could not be determined.";
            return false;
        }

        string baseName = Path.GetFileNameWithoutExtension(targetAssemblyPath);
        string runtimeConfigPath = Path.Combine(outputDir, baseName + ".runtimeconfig.json");
        string depsFilePath = Path.Combine(outputDir, baseName + ".deps.json");

        string hostPath = Path.Combine(AppContext.BaseDirectory, "XamlVisualEditor.Designer.PreviewerHost.dll");
        string transportUri = string.Empty;

        launchInfo = new PreviewerLaunchInfo(
            hostPath,
            targetAssemblyPath,
            runtimeConfigPath,
            depsFilePath,
            outputDir,
            transportUri,
            string.Empty,
            projectDir);

        return true;
    }

    private static void LogCompiledBindingsSetting(string assemblyPath, Action<string, string>? log)
    {
        if (!TryGetAssemblyMetadataValue(assemblyPath, "AvaloniaUseCompiledBindingsByDefault", out string? value))
        {
            return;
        }

        if (bool.TryParse(value, out bool enabled))
        {
            log?.Invoke("Info", $"Compiled bindings default: {enabled}");
        }
    }

    private static bool TryGetAssemblyMetadataValue(
        string assemblyPath,
        string key,
        out string? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
        {
            return false;
        }

        try
        {
            Assembly asm = Assembly.LoadFrom(assemblyPath);
            foreach (CustomAttributeData attr in asm.GetCustomAttributesData())
            {
                if (!string.Equals(attr.AttributeType.FullName, typeof(AssemblyMetadataAttribute).FullName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (attr.ConstructorArguments.Count != 2)
                {
                    continue;
                }

                string? attrKey = attr.ConstructorArguments[0].Value as string;
                if (!string.Equals(attrKey, key, StringComparison.Ordinal))
                {
                    continue;
                }

                value = attr.ConstructorArguments[1].Value as string;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private PreviewerTcpSession GetOrCreateSession(string xamlFilePath, Action<string, string>? log)
    {
        if (_sessions.TryGetValue(xamlFilePath, out PreviewerTcpSession? existing))
        {
            return existing;
        }

        PreviewerTcpSession session = new(xamlFilePath, log);
        session.ErrorReceived += error => PreviewerErrorReceived?.Invoke(error);
        _sessions[xamlFilePath] = session;
        return session;
    }

    private static ProjectModel? FindProjectForFile(WorkspaceModel workspace, string filePath)
    {
        foreach (ProjectModel project in workspace.Projects)
        {
            foreach (XamlFileModel file in project.XamlFiles)
            {
                if (string.Equals(file.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }

            foreach (ProjectFileModel file in project.Files)
            {
                if (string.Equals(file.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }
        }

        return null;
    }

    private static string? ResolveTargetAssemblyPath(ProjectModel project)
    {
        if (!string.IsNullOrWhiteSpace(project.OutputAssemblyPath))
        {
            return project.OutputAssemblyPath;
        }

        string? projectDir = Path.GetDirectoryName(project.ProjectPath);
        if (string.IsNullOrWhiteSpace(projectDir))
        {
            return null;
        }

        string[] searchRoots =
        {
            Path.Combine(projectDir, "bin", "Debug"),
            Path.Combine(projectDir, "bin", "Release")
        };

        string targetName = project.Name + ".dll";
        foreach (string root in searchRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                string? match = Directory.EnumerateFiles(root, targetName, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static string BuildXamlProjectPath(string xamlFilePath, string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
        {
            return xamlFilePath;
        }

        try
        {
            string relative = Path.GetRelativePath(projectDir, xamlFilePath);
            relative = relative.Replace(Path.DirectorySeparatorChar, '/');
            if (!relative.StartsWith('/'))
            {
                relative = "/" + relative;
            }

            return relative;
        }
        catch
        {
            return xamlFilePath;
        }
    }

    private static string NormalizeDesignDataContext(string xamlText)
    {
        if (string.IsNullOrWhiteSpace(xamlText))
        {
            return xamlText;
        }

        try
        {
            XDocument doc = XDocument.Parse(xamlText, LoadOptions.PreserveWhitespace);
            if (doc.Root is null)
            {
                return xamlText;
            }

            foreach (XElement element in doc.Descendants())
            {
                if (element.Attribute("Design.DataContext") is not null)
                {
                    continue;
                }

                foreach (XAttribute attr in element.Attributes())
                {
                    if (!IsDesignNamespace(attr.Name.NamespaceName))
                    {
                        continue;
                    }

                    if (!string.Equals(attr.Name.LocalName, "DataContext", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (LooksLikeMarkupExtension(attr.Value))
                    {
                        continue;
                    }

                    element.SetAttributeValue("Design.DataContext", attr.Value);
                    break;
                }
            }

            return doc.ToString(SaveOptions.DisableFormatting);
        }
        catch
        {
            return xamlText;
        }
    }

    private static bool IsDesignNamespace(string? xmlNamespace)
    {
        if (string.IsNullOrWhiteSpace(xmlNamespace))
        {
            return false;
        }

        return string.Equals(xmlNamespace, "http://schemas.microsoft.com/expression/blend/2008", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMarkupExtension(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal);
    }

    private static (double? Width, double? Height) TryGetDesignSize(string xamlText)
    {
        if (string.IsNullOrWhiteSpace(xamlText))
        {
            return (null, null);
        }

        double? width = TryMatchDesignDimension(xamlText, "DesignWidth")
            ?? TryMatchDesignDimension(xamlText, "Width");
        double? height = TryMatchDesignDimension(xamlText, "DesignHeight")
            ?? TryMatchDesignDimension(xamlText, "Height");

        return (width, height);
    }

    private static double? TryMatchDesignDimension(string text, string propertyName)
    {
        System.Text.RegularExpressions.Regex regex = new(
            $"\\b(?:\\w+:)?{propertyName}\\s*=\\s*\"(?<value>[0-9]+(?:\\.[0-9]+)?)\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        System.Text.RegularExpressions.Match match = regex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        string value = match.Groups["value"].Value;
        return double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double result)
            ? result
            : null;
    }
}

internal readonly record struct PreviewerLaunchInfo(
    string HostPath,
    string TargetAssemblyPath,
    string RuntimeConfigPath,
    string DepsFilePath,
    string WorkingDirectory,
    string TransportUri,
    string MethodArguments,
    string? ProjectDirectory)
{
    public bool HasRequiredAssets =>
        File.Exists(HostPath) &&
        File.Exists(TargetAssemblyPath) &&
        File.Exists(RuntimeConfigPath) &&
        File.Exists(DepsFilePath);
}

internal readonly record struct PreviewerLaunchResult(bool Success, string? ErrorMessage, Process? Process)
{
    public static PreviewerLaunchResult FromProcess(Process process) => new(true, null, process);

    public static PreviewerLaunchResult Fail(string message) => new(false, message, null);
}

public sealed record PreviewerErrorInfo(string Message, int? Line, int? Column, string? FilePath);

internal sealed class PreviewerTelemetryStore
{
    private readonly object _gate = new();
    private PreviewerTelemetryData _data;

    public PreviewerTelemetryStore()
    {
        _data = Load();
    }

    public void RecordStart(TimeSpan duration)
    {
        lock (_gate)
        {
            _data.StartCount++;
            _data.LastStartUtc = DateTimeOffset.UtcNow;
            _data.LastStartupMs = Math.Round(duration.TotalMilliseconds, 2);
            Save(_data);
        }
    }

    public void RecordCrash(int? exitCode)
    {
        lock (_gate)
        {
            _data.CrashCount++;
            _data.LastCrashUtc = DateTimeOffset.UtcNow;
            _data.LastExitCode = exitCode;
            Save(_data);
        }
    }

    public void RecordFailure(string? reason)
    {
        lock (_gate)
        {
            _data.FailureCount++;
            _data.LastFailureUtc = DateTimeOffset.UtcNow;
            _data.LastFailureReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
            Save(_data);
        }
    }

    private static PreviewerTelemetryData Load()
    {
        try
        {
            string path = GetTelemetryPath();
            if (!File.Exists(path))
            {
                return new PreviewerTelemetryData();
            }

            string json = File.ReadAllText(path);
            PreviewerTelemetryData? data = JsonSerializer.Deserialize<PreviewerTelemetryData>(json);
            return data ?? new PreviewerTelemetryData();
        }
        catch
        {
            return new PreviewerTelemetryData();
        }
    }

    private static void Save(PreviewerTelemetryData data)
    {
        try
        {
            string path = GetTelemetryPath();
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
        }
    }

    private static string GetTelemetryPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, "XamlVisualEditor");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "previewer-telemetry.json");
    }

    private sealed class PreviewerTelemetryData
    {
        public int StartCount { get; set; }
        public int CrashCount { get; set; }
        public int FailureCount { get; set; }
        public double LastStartupMs { get; set; }
        public DateTimeOffset? LastStartUtc { get; set; }
        public DateTimeOffset? LastCrashUtc { get; set; }
        public DateTimeOffset? LastFailureUtc { get; set; }
        public int? LastExitCode { get; set; }
        public string? LastFailureReason { get; set; }
    }
}
