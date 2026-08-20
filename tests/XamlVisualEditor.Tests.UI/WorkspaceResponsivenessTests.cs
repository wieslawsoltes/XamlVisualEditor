using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Workspace;

namespace XamlVisualEditor.Tests.UI;

public sealed class WorkspaceResponsivenessTests
{
    [AvaloniaFact]
    public async Task Workspace_Load_Uses_Nonblocking_Cli_And_Leaves_Shell_Responsive()
    {
        string root = Path.Combine(Path.GetTempPath(), $"xve-workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string projectPath = Path.Combine(root, "Test.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        ControllableDotNetCli dotNetCli = new();
        dotNetCli.BlockNextCall();
        using MainWindowViewModel viewModel = new(
            workspaceService: new StubWorkspaceService(),
            metadataService: new StubMetadataService(),
            dotNetCli: dotNetCli);

        try
        {
            Task loadTask = viewModel.OpenFileAsync(projectPath);
            await dotNetCli.BlockedCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(viewModel.IsWorkspaceLoading);
            Assert.Single(dotNetCli.Calls);
            Assert.Equal(new[] { "restore", projectPath }, dotNetCli.Calls[0].Arguments);
            Assert.Equal(root, dotNetCli.Calls[0].WorkingDirectory);

            await Dispatcher.UIThread.InvokeAsync(
                () => viewModel.AboutCommand.Execute().Subscribe(),
                DispatcherPriority.Background);
            Assert.Contains("Avalonia-based", viewModel.StatusText, StringComparison.Ordinal);

            dotNetCli.ReleaseBlockedCall();
            await loadTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(viewModel.IsWorkspaceLoading);
            Assert.True(viewModel.HasWorkspace);
            Assert.Equal(2, dotNetCli.Calls.Count);
            Assert.Equal(new[] { "build", projectPath }, dotNetCli.Calls[1].Arguments);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Workspace_Commands_Are_Single_Flight_And_Use_Exact_Arguments()
    {
        string root = Path.Combine(Path.GetTempPath(), $"xve-commands-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string projectPath = Path.Combine(root, "Test.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        ControllableDotNetCli dotNetCli = new();
        using MainWindowViewModel viewModel = new(
            workspaceService: new StubWorkspaceService(),
            metadataService: new StubMetadataService(),
            dotNetCli: dotNetCli);

        try
        {
            await viewModel.OpenFileAsync(projectPath);
            dotNetCli.Calls.Clear();
            dotNetCli.BlockNextCall();

            IWorkspaceCommands commands = viewModel;
            Task rebuildTask = commands.RebuildWorkspaceAsync(CancellationToken.None);
            await dotNetCli.BlockedCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(viewModel.IsWorkspaceCommandRunning);
            Assert.Single(dotNetCli.Calls);
            Assert.Equal(
                new[] { "build", projectPath, "-t:Rebuild" },
                dotNetCli.Calls[0].Arguments);

            await commands.CleanWorkspaceAsync(CancellationToken.None);
            Assert.Single(dotNetCli.Calls);

            dotNetCli.ReleaseBlockedCall();
            await rebuildTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(viewModel.IsWorkspaceCommandRunning);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class ControllableDotNetCli : IDotNetCli
    {
        private TaskCompletionSource<DotNetCliResult> _blockedCallCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _blockNextCall;

        public TaskCompletionSource<object?> BlockedCallStarted { get; private set; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<DotNetCliCall> Calls { get; } = new();

        public Task<DotNetCliResult> RunAsync(
            IReadOnlyList<string> args,
            string? workingDirectory,
            CancellationToken ct = default)
        {
            Calls.Add(new DotNetCliCall(new List<string>(args), workingDirectory));
            if (_blockNextCall)
            {
                _blockNextCall = false;
                BlockedCallStarted.TrySetResult(null);
                return _blockedCallCompletion.Task.WaitAsync(ct);
            }

            return Task.FromResult(new DotNetCliResult(0, "Build succeeded", string.Empty));
        }

        public void BlockNextCall()
        {
            _blockNextCall = true;
            BlockedCallStarted = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _blockedCallCompletion = new TaskCompletionSource<DotNetCliResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void ReleaseBlockedCall()
        {
            _blockedCallCompletion.TrySetResult(
                new DotNetCliResult(0, "Command succeeded", string.Empty));
        }
    }

    private sealed record DotNetCliCall(IReadOnlyList<string> Arguments, string? WorkingDirectory);

    private sealed class StubWorkspaceService : IWorkspaceService
    {
        public Task<WorkspaceModel> LoadSolutionAsync(string solutionPath, CancellationToken ct = default)
        {
            return Task.FromResult(CreateWorkspace(solutionPath));
        }

        public Task<WorkspaceModel> LoadProjectAsync(string projectPath, CancellationToken ct = default)
        {
            return Task.FromResult(CreateWorkspace(projectPath));
        }

        public WorkspaceModel CreateStandaloneWorkspace(string xamlFilePath)
        {
            return CreateWorkspace(xamlFilePath);
        }

        private static WorkspaceModel CreateWorkspace(string projectPath)
        {
            return new WorkspaceModel
            {
                Projects = new[]
                {
                    new ProjectModel
                    {
                        Name = "Test",
                        ProjectPath = projectPath,
                        XamlFiles = Array.Empty<XamlFileModel>(),
                        Files = Array.Empty<ProjectFileModel>(),
                        References = Array.Empty<AssemblyReference>(),
                        OutputAssemblyPath = null
                    }
                }
            };
        }
    }

    private sealed class StubMetadataService : ITypeMetadataService
    {
        public TypeMetadata? GetType(string xmlNamespace, string typeName) => null;

        public IReadOnlyList<TypeMetadata> GetAvailableTypes(string? xmlNamespace = null) =>
            Array.Empty<TypeMetadata>();

        public IReadOnlyList<PropertyMetadata> GetProperties(TypeMetadata type) =>
            Array.Empty<PropertyMetadata>();

        public IReadOnlyList<EventMetadata> GetEvents(TypeMetadata type) =>
            Array.Empty<EventMetadata>();

        public IReadOnlyList<string> GetAvailableNamespaces() => Array.Empty<string>();

        public void LoadAssembly(string assemblyPath)
        {
        }

        public void LoadAssemblies(IEnumerable<string> assemblyPaths)
        {
        }

        public Type? ResolveClrType(TypeMetadata type) => null;
    }
}
