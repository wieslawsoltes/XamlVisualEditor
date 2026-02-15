using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Core;
using XamlVisualEditor.AcpExtension;
using XamlVisualEditor.AcpExtension.Views;
using XamlVisualEditor.App.Views;
using XamlVisualEditor.CodeEditor;
using XamlVisualEditor.Collaboration.UI;
using XamlVisualEditor.CollaborationExtension;
using XamlVisualEditor.CollaborationExtension.Views;
using XamlVisualEditor.Designer.Core;
using XamlVisualEditor.GitExtension;
using XamlVisualEditor.GitExtension.Views;
using XamlVisualEditor.IdeBridgeExtension;
using XamlVisualEditor.IdeBridgeExtension.Views;
using XamlVisualEditor.LspSettingsExtension;
using XamlVisualEditor.LspSettingsExtension.Views;
using XamlVisualEditor.McpExtension;
using XamlVisualEditor.McpExtension.Views;
using XamlVisualEditor.NavigationExtension;
using XamlVisualEditor.NavigationExtension.Views;
using XamlVisualEditor.OutputExtension;
using XamlVisualEditor.OutputExtension.Views;
using XamlVisualEditor.PropertyEditorExtension;
using XamlVisualEditor.PropertyEditorExtension.Views;
using XamlVisualEditor.ToolboxExtension;
using XamlVisualEditor.ToolboxExtension.Views;
using XamlVisualEditor.DebugSettingsExtension;
using XamlVisualEditor.DebugSettingsExtension.Views;
using XamlVisualEditor.Shell;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Terminal.Avalonia.Views;
using XamlVisualEditor.TreeInspectorExtension;
using XamlVisualEditor.TreeInspectorExtension.Views;
using XamlVisualEditor.TreeView;

namespace XamlVisualEditor.App;

/// <summary>
/// Non-reflection ViewLocator that maps ViewModel types to View types.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    /// <inheritdoc />
    public Control Build(object? param)
    {
        if (param is IDockable dockable)
        {
            switch (dockable)
            {
                case DesignerDocument doc:
                    return new DesignerDocumentView { DataContext = doc.DocumentViewModel };
                case TextDocument doc:
                    return new TextFileView { DataContext = doc.DocumentViewModel };
                case InfiniteCanvasDocument doc:
                    return new InfiniteCanvasView { DataContext = doc.CanvasViewModel };
                case CanvasMdiDocument doc when doc.DocumentViewModel is DesignerDocumentViewModel designer:
                    return new DesignerDocumentView { DataContext = designer };
                case CanvasMdiDocument doc when doc.DocumentViewModel is TextDocumentViewModel text:
                    return new TextFileView { DataContext = text };
                case SolutionExplorerTool tool:
                    return new SolutionExplorerView { DataContext = tool.SolutionExplorerViewModel };
                case OutputTool tool:
                    return new OutputView { DataContext = tool.OutputViewModel };
                case TerminalTool tool:
                    return new TerminalToolView { DataContext = tool };
                case BreakpointsTool tool:
                    return new BreakpointsView { DataContext = tool.BreakpointsViewModel };
                case CallStackTool tool:
                    return new CallStackView { DataContext = tool.CallStackViewModel };
                case LocalsTool tool:
                    return new LocalsView { DataContext = tool.LocalsViewModel };
                case WatchesTool tool:
                    return new WatchesView { DataContext = tool.WatchesViewModel };
                case ExtensionTool tool:
                    return new ExtensionToolView { DataContext = tool };
                case ExtensionManagerTool tool:
                    return new ExtensionManagerView { DataContext = tool.ExtensionManagerViewModel };
            }
        }

        return param switch
        {
            DesignerDocumentViewModel => new DesignerDocumentView(),
            SolutionExplorerViewModel => new SolutionExplorerView(),
            ToolboxViewModel => new ToolboxView(),
            OutputViewModel => new OutputView(),
            AcpToolViewModel => new AcpToolView(),
            DebugSettingsViewModel => new DebugSettingsView(),
            LspSettingsViewModel => new LspSettingsView(),
            DebugSettingsPanelViewModel => new DebugSettingsPanelView(),
            LspSettingsPanelViewModel => new LspSettingsPanelView(),
            BreakpointsViewModel => new BreakpointsView(),
            CallStackViewModel => new CallStackView(),
            LocalsViewModel => new LocalsView(),
            WatchesViewModel => new WatchesView(),
            CodeEditorViewModel => new XamlCodeEditorView(),
            TextDocumentViewModel => new TextFileView(),
            InfiniteCanvasViewModel => new InfiniteCanvasView(),
            DesignSurfaceViewModel => new DesignSurfaceView(),
            CollaborationPanelToolViewModel => new CollaborationToolPanelView(),
            CollaborationPanelViewModel => new CollaborationPanelView(),
            VisualTreeNodeViewModel => new VisualTreePanelView(),
            LogicalTreeNodeViewModel => new LogicalTreePanelView(),
            AnimationEditorViewModel => new AnimationEditorView(),
            TerminalViewModel => new TerminalView(),
            GitPanelViewModel => new GitPanelView(),
            IdeBridgePanelViewModel => new IdeBridgePanelView(),
            McpPanelViewModel => new McpPanelView(),
            ReferencesPanelViewModel => new ReferencesPanelView(),
            OutputPanelViewModel => new OutputPanelView(),
            ProblemsPanelViewModel => new ProblemsPanelView(),
            PropertyEditorPanelViewModel => new PropertyEditorPanelView(),
            ToolboxPanelViewModel => new ToolboxPanelView(),
            TreeInspectorPanelViewModel => new TreeInspectorPanelView(),
            ExtensionTreeViewModel => new ExtensionTreeView(),
            ExtensionWebviewViewModel => new ExtensionWebviewView(),
            ExtensionManagerViewModel => new ExtensionManagerView(),
            _ => new TextBlock { Text = $"No view for {param?.GetType().FullName ?? "null"}" }
        };
    }

    /// <inheritdoc />
    public bool Match(object? data)
    {
        // Match known ViewModel types
        return data is IDockable
            or DesignerDocumentViewModel
            or SolutionExplorerViewModel
            or ToolboxViewModel
            or OutputViewModel
            or AcpToolViewModel
            or DebugSettingsViewModel
            or LspSettingsViewModel
            or DebugSettingsPanelViewModel
            or LspSettingsPanelViewModel
            or BreakpointsViewModel
            or CallStackViewModel
            or LocalsViewModel
            or WatchesViewModel
            or ReferencesViewModel
            or CodeEditorViewModel
            or TextDocumentViewModel
            or InfiniteCanvasViewModel
            or DesignSurfaceViewModel
            or CollaborationPanelToolViewModel
            or CollaborationPanelViewModel
            or VisualTreeNodeViewModel
            or LogicalTreeNodeViewModel
            or AnimationEditorViewModel
            or TerminalViewModel
            or GitPanelViewModel
            or IdeBridgePanelViewModel
            or McpPanelViewModel
            or OutputPanelViewModel
            or PropertyEditorPanelViewModel
            or ToolboxPanelViewModel
            or TreeInspectorPanelViewModel
            or ExtensionTreeViewModel
            or ExtensionWebviewViewModel
            or ExtensionManagerViewModel;
    }
}
