using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Sync;
using XamlVisualEditor.Xaml.Ast;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class DesignerNavigationTests
{
    [AvaloniaFact]
    public async Task NavigateToDefinitionAsync_OpensMatchingXamlFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string mainPath = Path.Combine(tempDir, "MainView.axaml");
            string controlPath = Path.Combine(tempDir, "CustomControl.axaml");

            File.WriteAllText(mainPath, """
                <UserControl xmlns="https://github.com/avaloniaui"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             xmlns:views="clr-namespace:MyApp.Views"
                             x:Class="MyApp.Views.MainView">
                    <views:CustomControl />
                </UserControl>
                """);

            File.WriteAllText(controlPath, """
                <UserControl xmlns="https://github.com/avaloniaui"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             x:Class="MyApp.Views.CustomControl" />
                """);

            WorkspaceModel workspace = new()
            {
                Projects = new[]
                {
                    new ProjectModel
                    {
                        Name = "TestProject",
                        ProjectPath = Path.Combine(tempDir, "TestProject.csproj"),
                        XamlFiles = new[]
                        {
                            new XamlFileModel { FilePath = mainPath, RelativePath = "MainView.axaml" },
                            new XamlFileModel { FilePath = controlPath, RelativePath = "CustomControl.axaml" }
                        },
                        Files = new[]
                        {
                            new ProjectFileModel { FilePath = mainPath, RelativePath = "MainView.axaml" },
                            new ProjectFileModel { FilePath = controlPath, RelativePath = "CustomControl.axaml" }
                        },
                        References = Array.Empty<AssemblyReference>()
                    }
                },
                ProjectFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            string? openedPath = null;
            Func<string, Task> openFileAsync = path =>
            {
                openedPath = path;
                return Task.CompletedTask;
            };

            TypeMetadata customControl = new()
            {
                FullName = "MyApp.Views.CustomControl",
                Name = "CustomControl",
                XmlNamespace = "clr-namespace:MyApp.Views",
                ClrNamespace = "MyApp.Views",
                AssemblyName = "MyApp"
            };

            DesignerDocumentViewModel doc = new(
                mainPath,
                new StubMetadataService(customControl),
                () => workspace,
                openFileAsync);

            await doc.LoadAsync();

            MutableAstDocument? document = doc.SyncEngine.CurrentDocument;
            MutableAstObjectNode? node = FindObjectNode(document?.Root, "CustomControl");
            Assert.NotNull(node);

            doc.SetSelectedNode(node!.Id, SyncSource.DesignSurface);
            await doc.NavigateToDefinitionAsync();

            Assert.Equal(controlPath, openedPath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private static MutableAstObjectNode? FindObjectNode(MutableAstObjectNode? root, string typeName)
    {
        if (root is null)
        {
            return null;
        }

        if (string.Equals(root.TypeName, typeName, StringComparison.Ordinal))
        {
            return root;
        }

        foreach (MutableAstObjectNode child in root.Children.OfType<MutableAstObjectNode>())
        {
            MutableAstObjectNode? match = FindObjectNode(child, typeName);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private sealed class StubMetadataService : ITypeMetadataService
    {
        private readonly Dictionary<(string XmlNamespace, string Name), TypeMetadata> _types;

        public StubMetadataService(params TypeMetadata[] types)
        {
            _types = types.ToDictionary(
                t => (t.XmlNamespace, t.Name),
                t => t,
                StringTupleComparer.Ordinal);
        }

        public TypeMetadata? GetType(string xmlNamespace, string typeName)
        {
            return _types.TryGetValue((xmlNamespace, typeName), out TypeMetadata? type) ? type : null;
        }

        public IReadOnlyList<TypeMetadata> GetAvailableTypes(string? xmlNamespace = null)
        {
            return Array.Empty<TypeMetadata>();
        }

        public IReadOnlyList<PropertyMetadata> GetProperties(TypeMetadata type)
        {
            return Array.Empty<PropertyMetadata>();
        }

        public IReadOnlyList<EventMetadata> GetEvents(TypeMetadata type)
        {
            return Array.Empty<EventMetadata>();
        }

        public IReadOnlyList<string> GetAvailableNamespaces()
        {
            return Array.Empty<string>();
        }

        public void LoadAssembly(string assemblyPath)
        {
        }

        public void LoadAssemblies(IEnumerable<string> assemblyPaths)
        {
        }

        public Type? ResolveClrType(TypeMetadata type)
        {
            return null;
        }

        private sealed class StringTupleComparer : IEqualityComparer<(string XmlNamespace, string Name)>
        {
            public static StringTupleComparer Ordinal { get; } = new();

            public bool Equals((string XmlNamespace, string Name) x, (string XmlNamespace, string Name) y)
            {
                return string.Equals(x.XmlNamespace, y.XmlNamespace, StringComparison.Ordinal)
                    && string.Equals(x.Name, y.Name, StringComparison.Ordinal);
            }

            public int GetHashCode((string XmlNamespace, string Name) obj)
            {
                return HashCode.Combine(
                    StringComparer.Ordinal.GetHashCode(obj.XmlNamespace),
                    StringComparer.Ordinal.GetHashCode(obj.Name));
            }
        }
    }
}
