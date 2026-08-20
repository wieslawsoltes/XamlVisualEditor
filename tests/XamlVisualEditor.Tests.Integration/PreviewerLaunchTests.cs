using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using XamlVisualEditor.Core;
using XamlVisualEditor.Workspace;
using Xunit;

namespace XamlVisualEditor.Tests.Integration;

public sealed class PreviewerLaunchTests
{
    [Fact]
    public async Task PreviewerLaunchService_StartsPreviewerForAppProject()
    {
        string repoRoot = FindRepoRoot();
        string appProjectPath = Path.Combine(repoRoot, "src", "XamlVisualEditor.App", "XamlVisualEditor.App.csproj");
        string xamlFilePath = Path.Combine(repoRoot, "src", "XamlVisualEditor.App", "MainWindow.axaml");

        await BuildProjectAsync(appProjectPath);

        string? outputAssemblyPath = FindOutputAssembly(Path.GetDirectoryName(appProjectPath)!);
        Assert.False(string.IsNullOrWhiteSpace(outputAssemblyPath));
        Assert.True(File.Exists(outputAssemblyPath!));

        WorkspaceModel workspace = new()
        {
            Projects = new[]
            {
                new ProjectModel
                {
                    Name = "XamlVisualEditor.App",
                    ProjectPath = appProjectPath,
                    XamlFiles = new[]
                    {
                        new XamlFileModel { FilePath = xamlFilePath, RelativePath = "MainWindow.axaml" }
                    },
                    Files = Array.Empty<ProjectFileModel>(),
                    References = Array.Empty<AssemblyReference>(),
                    OutputAssemblyPath = outputAssemblyPath
                }
            },
            ProjectFolders = new System.Collections.Generic.Dictionary<string, string>()
        };

        object previewerService = CreatePreviewerLaunchService();
        try
        {
            object? result = await StartPreviewerAsync(previewerService, xamlFilePath, File.ReadAllText(xamlFilePath), workspace);
            bool success = GetResultProperty<bool>(result, "Success");
            string? errorMessage = GetResultProperty<string>(result, "ErrorMessage");

            Assert.True(success, errorMessage ?? "Previewer launch failed.");
        }
        finally
        {
            InvokeDispose(previewerService);
        }
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            string candidate = Path.Combine(current, "XamlVisualEditor.slnx");
            if (File.Exists(candidate))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Failed to locate repository root.");
    }

    private static async Task BuildProjectAsync(string projectPath)
    {
        DotNetCliRunner dotNetCli = new();
        DotNetCliResult result = await dotNetCli.RunAsync(
            new[]
            {
                "build",
                projectPath,
                "-c",
                "Debug"
            },
            Path.GetDirectoryName(projectPath));

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"dotnet build failed: {result.StandardError}\n{result.StandardOutput}");
        }
    }

    private static string? FindOutputAssembly(string projectDir)
    {
        string binRoot = Path.Combine(projectDir, "bin", "Debug");
        if (!Directory.Exists(binRoot))
        {
            return null;
        }

        string[] matches = Directory.GetFiles(binRoot, "XamlVisualEditor.App.dll", SearchOption.AllDirectories);
        return matches.FirstOrDefault();
    }

    private static object CreatePreviewerLaunchService()
    {
        Type? type = typeof(XamlVisualEditor.Shell.ViewModels.MainWindowViewModel).Assembly
            .GetType("XamlVisualEditor.Shell.ViewModels.PreviewerLaunchService");
        Assert.NotNull(type);
        return Activator.CreateInstance(type!, nonPublic: true) ?? throw new InvalidOperationException();
    }

    private static async Task<object?> StartPreviewerAsync(
        object previewerService,
        string xamlFilePath,
        string xamlText,
        WorkspaceModel workspace)
    {
        MethodInfo? method = previewerService.GetType().GetMethod("StartPreviewerAsync");
        Assert.NotNull(method);

        object? task = method!.Invoke(previewerService, new object?[]
        {
            xamlFilePath,
            xamlText,
            workspace,
            null,
            null,
            null
        });

        Assert.NotNull(task);
        Task awaited = (Task)task!;
        await awaited.ConfigureAwait(false);

        PropertyInfo? resultProperty = awaited.GetType().GetProperty("Result");
        Assert.NotNull(resultProperty);
        return resultProperty!.GetValue(awaited);
    }

    private static T GetResultProperty<T>(object? result, string name)
    {
        Assert.NotNull(result);
        PropertyInfo? property = result!.GetType().GetProperty(name);
        Assert.NotNull(property);
        object? value = property!.GetValue(result);
        return value is T typed ? typed : default!;
    }

    private static void InvokeDispose(object previewerService)
    {
        MethodInfo? method = previewerService.GetType().GetMethod("Dispose");
        method?.Invoke(previewerService, null);
    }
}
