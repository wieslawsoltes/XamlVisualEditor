using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using XamlVisualEditor.App.Views;
using XamlVisualEditor.CodeEditor;
using XamlVisualEditor.Collaboration.UI;
using XamlVisualEditor.Designer.Core;
using XamlVisualEditor.PropertyEditor;
using XamlVisualEditor.Shell.ViewModels;
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
        return param switch
        {
            DesignerDocumentViewModel => new DesignerDocumentView(),
            ToolboxViewModel => new ToolboxView(),
            OutputViewModel => new OutputView(),
            CodeEditorViewModel => new XamlCodeEditorView(),
            DesignSurfaceViewModel => new DesignSurfaceView(),
            PropertyEditorViewModel => new PropertyEditorView(),
            CollaborationPanelViewModel => new CollaborationPanelView(),
            VisualTreeNodeViewModel => new VisualTreePanelView(),
            LogicalTreeNodeViewModel => new LogicalTreePanelView(),
            _ => new TextBlock { Text = $"No view for {param?.GetType().FullName ?? "null"}" }
        };
    }

    /// <inheritdoc />
    public bool Match(object? data)
    {
        // Match known ViewModel types
        return data is DesignerDocumentViewModel
            or ToolboxViewModel
            or OutputViewModel
            or CodeEditorViewModel
            or DesignSurfaceViewModel
            or PropertyEditorViewModel
            or CollaborationPanelViewModel
            or VisualTreeNodeViewModel
            or LogicalTreeNodeViewModel;
    }
}
