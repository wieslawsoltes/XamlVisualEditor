using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Core;
using XamlVisualEditor.AcpExtension;
using XamlVisualEditor.AcpExtension.Views;
using XamlVisualEditor.App.Views;
using XamlVisualEditor.CodeEditor;
using XamlVisualEditor.Collaboration.UI;
using XamlVisualEditor.Designer.Core;
using XamlVisualEditor.GitExtension;
using XamlVisualEditor.GitExtension.Views;
using XamlVisualEditor.PropertyEditor;
using XamlVisualEditor.Shell;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Terminal.Avalonia.Views;
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
                case ToolboxTool tool:
                    return new ToolboxView { DataContext = tool.ToolboxViewModel };
                case SolutionExplorerTool tool:
                    return new SolutionExplorerView { DataContext = tool.SolutionExplorerViewModel };
                case PropertyEditorTool tool:
                    return new PropertyEditorToolView { DataContext = tool };
                case VisualTreeTool tool:
                    return new VisualTreeToolView { DataContext = tool };
                case LogicalTreeTool tool:
                    return new LogicalTreeToolView { DataContext = tool };
                case OutputTool tool:
                    return new OutputView { DataContext = tool.OutputViewModel };
                case TerminalTool tool:
                    return new TerminalView { DataContext = tool.TerminalViewModel };
                case DebugSettingsTool tool:
                    return new DebugSettingsView { DataContext = tool.DebugSettingsViewModel };
                case LspSettingsTool tool:
                    return new LspSettingsView { DataContext = tool.LspSettingsViewModel };
                case BreakpointsTool tool:
                    return new BreakpointsView { DataContext = tool.BreakpointsViewModel };
                case CallStackTool tool:
                    return new CallStackView { DataContext = tool.CallStackViewModel };
                case LocalsTool tool:
                    return new LocalsView { DataContext = tool.LocalsViewModel };
                case WatchesTool tool:
                    return new WatchesView { DataContext = tool.WatchesViewModel };
                case ReferencesTool tool:
                    return new ReferencesView { DataContext = tool.ReferencesViewModel };
                case CollaborationTool tool:
                    return new CollaborationPanelView { DataContext = tool.CollaborationViewModel };
                case AnimationEditorTool tool:
                    return new AnimationEditorView { DataContext = tool.AnimationEditor };
                case ExtensionTool tool:
                    return new ExtensionToolView { DataContext = tool };
                case ExtensionManagerTool tool:
                    return new ExtensionManagerView { DataContext = tool.ExtensionManagerViewModel };
            }
        }

        return param switch
        {
            DesignerDocumentViewModel => new DesignerDocumentView(),
            ToolboxViewModel => new ToolboxView(),
            OutputViewModel => new OutputView(),
            AcpToolViewModel => new AcpToolView(),
            DebugSettingsViewModel => new DebugSettingsView(),
            LspSettingsViewModel => new LspSettingsView(),
            BreakpointsViewModel => new BreakpointsView(),
            CallStackViewModel => new CallStackView(),
            LocalsViewModel => new LocalsView(),
            WatchesViewModel => new WatchesView(),
            ReferencesViewModel => new ReferencesView(),
            CodeEditorViewModel => new XamlCodeEditorView(),
            TextDocumentViewModel => new TextFileView(),
            InfiniteCanvasViewModel => new InfiniteCanvasView(),
            DesignSurfaceViewModel => new DesignSurfaceView(),
            PropertyEditorViewModel => new PropertyEditorView(),
            CollaborationPanelViewModel => new CollaborationPanelView(),
            VisualTreeNodeViewModel => new VisualTreePanelView(),
            LogicalTreeNodeViewModel => new LogicalTreePanelView(),
            AnimationEditorViewModel => new AnimationEditorView(),
            TerminalViewModel => new TerminalView(),
            GitPanelViewModel => new GitPanelView(),
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
            or ToolboxViewModel
            or OutputViewModel
            or AcpToolViewModel
            or DebugSettingsViewModel
            or LspSettingsViewModel
            or BreakpointsViewModel
            or CallStackViewModel
            or LocalsViewModel
            or WatchesViewModel
            or ReferencesViewModel
            or CodeEditorViewModel
            or TextDocumentViewModel
            or InfiniteCanvasViewModel
            or DesignSurfaceViewModel
            or PropertyEditorViewModel
            or CollaborationPanelViewModel
            or VisualTreeNodeViewModel
            or LogicalTreeNodeViewModel
            or AnimationEditorViewModel
            or TerminalViewModel
            or GitPanelViewModel
            or ExtensionTreeViewModel
            or ExtensionWebviewViewModel
            or ExtensionManagerViewModel;
    }
}
