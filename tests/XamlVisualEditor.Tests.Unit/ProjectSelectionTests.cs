using System;
using System.Collections.Generic;
using System.Linq;
using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class ProjectSelectionTests
{
    [Fact]
    public void ResolveTargetAssemblyPath_Uses_OutputAssemblyPath()
    {
        ProjectModel project = CreateProject(
            name: "App",
            projectPath: "/tmp/App/App.csproj",
            outputAssemblyPath: "/tmp/App/bin/Debug/net6.0/App.dll",
            targetFramework: "net6.0",
            isExecutable: true);

        string? resolved = MainWindowViewModel.ResolveTargetAssemblyPath(project);

        Assert.Equal(project.OutputAssemblyPath, resolved);
    }

    [Fact]
    public void SetStartupProject_Matches_TargetFramework()
    {
        ProjectModel net6Project = CreateProject(
            name: "App (net6.0)",
            projectPath: "/tmp/App/App.csproj",
            outputAssemblyPath: "/tmp/App/bin/Debug/net6.0/App.dll",
            targetFramework: "net6.0",
            isExecutable: true);
        ProjectModel net8Project = CreateProject(
            name: "App (net8.0)",
            projectPath: "/tmp/App/App.csproj",
            outputAssemblyPath: "/tmp/App/bin/Debug/net8.0/App.dll",
            targetFramework: "net8.0",
            isExecutable: true);

        WorkspaceModel workspace = new()
        {
            Projects = new[] { net6Project, net8Project },
            ProjectFolders = new Dictionary<string, string>()
        };

        SolutionExplorerViewModel explorer = new();
        explorer.LoadWorkspace(workspace, "Solution");
        explorer.SetStartupProject(net8Project);

        List<SolutionExplorerNodeViewModel> projectNodes = EnumerateNodes(explorer.Root!)
            .Where(node => node.Kind == SolutionExplorerNodeKind.Project)
            .ToList();

        Assert.Single(projectNodes);
        Assert.True(projectNodes[0].IsStartupProject);
        Assert.Same(net8Project, projectNodes[0].Project);
    }

    [Fact]
    public void LoadWorkspace_UsesSystemIcons_WhenAvailable()
    {
        object projectIcon = new();
        object fileIcon = new();
        RecordingSystemIconService icons = new((path, isDirectory, fallback) =>
        {
            if (string.Equals(path, "/tmp/App/App.csproj", StringComparison.OrdinalIgnoreCase))
            {
                return projectIcon;
            }

            if (string.Equals(path, "/tmp/App/Program.cs", StringComparison.OrdinalIgnoreCase))
            {
                return fileIcon;
            }

            return fallback;
        });

        WorkspaceModel workspace = new()
        {
            Projects = new[]
            {
                new ProjectModel
                {
                    Name = "App",
                    ProjectPath = "/tmp/App/App.csproj",
                    Files = new[]
                    {
                        new ProjectFileModel
                        {
                            FilePath = "/tmp/App/Program.cs",
                            RelativePath = "Program.cs"
                        }
                    },
                    References = Array.Empty<AssemblyReference>(),
                    XamlFiles = Array.Empty<XamlFileModel>()
                }
            },
            ProjectFolders = new Dictionary<string, string>()
        };

        SolutionExplorerViewModel explorer = new(icons);
        explorer.LoadWorkspace(workspace, "Solution");

        SolutionExplorerNodeViewModel projectNode = Assert.Single(
            EnumerateNodes(explorer.Root!),
            node => node.Kind == SolutionExplorerNodeKind.Project);
        SolutionExplorerNodeViewModel fileNode = Assert.Single(
            EnumerateNodes(explorer.Root!),
            node => node.Kind == SolutionExplorerNodeKind.File);

        Assert.Same(projectIcon, projectNode.Icon);
        Assert.Same(fileIcon, fileNode.Icon);
    }

    [Fact]
    public void LoadWorkspace_UsesFallbackIcons_WhenSystemIconsUnavailable()
    {
        RecordingSystemIconService icons = new((_, _, _) => null);
        WorkspaceModel workspace = new()
        {
            Projects = new[]
            {
                new ProjectModel
                {
                    Name = "App",
                    ProjectPath = "/tmp/App/App.csproj",
                    Files = new[]
                    {
                        new ProjectFileModel
                        {
                            FilePath = "/tmp/App/settings.json",
                            RelativePath = "settings.json"
                        }
                    },
                    References = Array.Empty<AssemblyReference>(),
                    XamlFiles = Array.Empty<XamlFileModel>()
                }
            },
            ProjectFolders = new Dictionary<string, string>()
        };

        SolutionExplorerViewModel explorer = new(icons);
        explorer.LoadWorkspace(workspace, "Solution");

        SolutionExplorerNodeViewModel projectNode = Assert.Single(
            EnumerateNodes(explorer.Root!),
            node => node.Kind == SolutionExplorerNodeKind.Project);
        SolutionExplorerNodeViewModel fileNode = Assert.Single(
            EnumerateNodes(explorer.Root!),
            node => node.Kind == SolutionExplorerNodeKind.File);

        Assert.Equal("📦", projectNode.Icon);
        Assert.Equal("🧾", fileNode.Icon);
    }

    private static ProjectModel CreateProject(
        string name,
        string projectPath,
        string outputAssemblyPath,
        string targetFramework,
        bool isExecutable)
    {
        return new ProjectModel
        {
            Name = name,
            ProjectPath = projectPath,
            XamlFiles = Array.Empty<XamlFileModel>(),
            Files = Array.Empty<ProjectFileModel>(),
            References = Array.Empty<AssemblyReference>(),
            OutputAssemblyPath = outputAssemblyPath,
            TargetFramework = targetFramework,
            IsExecutable = isExecutable
        };
    }

    private static IEnumerable<SolutionExplorerNodeViewModel> EnumerateNodes(SolutionExplorerNodeViewModel root)
    {
        Queue<SolutionExplorerNodeViewModel> queue = new();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            SolutionExplorerNodeViewModel node = queue.Dequeue();
            yield return node;
            foreach (SolutionExplorerNodeViewModel child in node.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    private sealed class RecordingSystemIconService : ISystemIconService
    {
        private readonly Func<string?, bool, object?, object?> _resolver;

        public RecordingSystemIconService(Func<string?, bool, object?, object?> resolver)
        {
            _resolver = resolver;
        }

        public object? GetIcon(string? path, bool isDirectory, object? fallbackIcon = null, int iconSize = 16)
            => _resolver(path, isDirectory, fallbackIcon);

        public object? GetFileIcon(string? path, object? fallbackIcon = null, int iconSize = 16)
            => _resolver(path, false, fallbackIcon);

        public object? GetFolderIcon(string? path, object? fallbackIcon = null, int iconSize = 16)
            => _resolver(path, true, fallbackIcon);
    }
}
