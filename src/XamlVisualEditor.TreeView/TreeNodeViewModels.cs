using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.TreeView;

/// <summary>
/// ViewModel for a node in the visual tree panel.
/// </summary>
public sealed class VisualTreeNodeViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the AST node ID this tree node represents.
    /// </summary>
    public Guid AstNodeId { get; }

    /// <summary>
    /// Gets or sets the control type name.
    /// </summary>
    [Reactive]
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the x:Name of the element, if any.
    /// </summary>
    [Reactive]
    public string? Name { get; set; }

    /// <summary>
    /// Gets the display text for this node (reactive, updates when TypeName or Name changes).
    /// </summary>
    public string DisplayText => _displayText.Value;
    private readonly ObservableAsPropertyHelper<string> _displayText;

    /// <summary>
    /// Gets or sets whether this node is selected.
    /// </summary>
    [Reactive]
    public bool IsSelected { get; set; }

    /// <summary>
    /// Gets or sets whether this node is expanded.
    /// </summary>
    [Reactive]
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// Gets the child nodes.
    /// </summary>
    public ObservableCollection<VisualTreeNodeViewModel> Children { get; } = new();

    /// <summary>
    /// Fires when this node is selected (for upstream sync).
    /// </summary>
    public event Action<Guid>? NodeSelected;

    /// <summary>
    /// Command to copy this node's type name.
    /// </summary>
    public ReactiveCommand<Unit, string> CopyTypeNameCommand { get; }

    public VisualTreeNodeViewModel(Guid astNodeId)
    {
        AstNodeId = astNodeId;

        _displayText = this.WhenAnyValue(x => x.TypeName, x => x.Name)
            .Select(t => string.IsNullOrEmpty(t.Item2) ? t.Item1 : $"{t.Item1} ({t.Item2})")
            .ToProperty(this, x => x.DisplayText);

        // Watch selection changes and fire event
        this.WhenAnyValue(x => x.IsSelected)
            .Subscribe(selected =>
            {
                if (selected)
                {
                    NodeSelected?.Invoke(AstNodeId);
                }
            });

        CopyTypeNameCommand = ReactiveCommand.Create(() => TypeName);
    }

    /// <summary>
    /// Finds a tree node by AST node ID recursively.
    /// </summary>
    public VisualTreeNodeViewModel? FindByNodeId(Guid nodeId)
    {
        if (AstNodeId == nodeId)
        {
            return this;
        }

        foreach (VisualTreeNodeViewModel child in Children)
        {
            VisualTreeNodeViewModel? found = child.FindByNodeId(nodeId);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Selects a specific node by AST ID, deselecting all others.
    /// </summary>
    public void SelectByNodeId(Guid nodeId)
    {
        IsSelected = AstNodeId == nodeId;
        foreach (VisualTreeNodeViewModel child in Children)
        {
            child.SelectByNodeId(nodeId);
        }
    }

    /// <summary>
    /// Collects expanded node IDs into the provided set.
    /// </summary>
    public void CollectExpandedIds(ISet<Guid> expanded)
    {
        if (IsExpanded)
        {
            expanded.Add(AstNodeId);
        }

        foreach (VisualTreeNodeViewModel child in Children)
        {
            child.CollectExpandedIds(expanded);
        }
    }

    /// <summary>
    /// Applies expanded state from the provided set.
    /// </summary>
    public void ApplyExpandedIds(ISet<Guid> expanded)
    {
        IsExpanded = expanded.Contains(AstNodeId);
        foreach (VisualTreeNodeViewModel child in Children)
        {
            child.ApplyExpandedIds(expanded);
        }
    }

    /// <summary>
    /// Expands all parents along the path to the specified node.
    /// </summary>
    public bool ExpandPathToNode(Guid nodeId)
    {
        if (AstNodeId == nodeId)
        {
            IsExpanded = true;
            return true;
        }

        foreach (VisualTreeNodeViewModel child in Children)
        {
            if (child.ExpandPathToNode(nodeId))
            {
                IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a visual tree from a mutable AST document.
    /// </summary>
    public static VisualTreeNodeViewModel? FromAstDocument(MutableAstDocument? document)
    {
        if (document?.Root is null)
        {
            return null;
        }

        return FromAstNode(document.Root);
    }

    /// <summary>
    /// Creates a visual tree node from a mutable AST object node.
    /// </summary>
    public static VisualTreeNodeViewModel FromAstNode(MutableAstObjectNode node)
    {
        VisualTreeNodeViewModel vm = new(node.Id)
        {
            TypeName = node.TypeName,
            Name = node.GetPropertyValue("Name") ?? node.GetPropertyValue("x:Name")
        };

        foreach (MutableAstNode child in node.Children)
        {
            if (child is MutableAstObjectNode childObj)
            {
                vm.Children.Add(FromAstNode(childObj));
            }
        }

        return vm;
    }
}

/// <summary>
/// ViewModel for a node in the logical tree panel.
/// </summary>
public sealed class LogicalTreeNodeViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the AST node ID this tree node represents.
    /// </summary>
    public Guid AstNodeId { get; }

    /// <summary>
    /// Gets or sets the control type name.
    /// </summary>
    [Reactive]
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the x:Name of the element, if any.
    /// </summary>
    [Reactive]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets a preview of the content (e.g., text content).
    /// </summary>
    [Reactive]
    public string? ContentPreview { get; set; }

    /// <summary>
    /// Gets the display text for this node (reactive, updates when TypeName, Name, or ContentPreview changes).
    /// </summary>
    public string DisplayText => _displayText.Value;
    private readonly ObservableAsPropertyHelper<string> _displayText;

    /// <summary>
    /// Gets or sets whether this node is selected.
    /// </summary>
    [Reactive]
    public bool IsSelected { get; set; }

    /// <summary>
    /// Gets or sets whether this node is expanded.
    /// </summary>
    [Reactive]
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// Gets the child nodes.
    /// </summary>
    public ObservableCollection<LogicalTreeNodeViewModel> Children { get; } = new();

    /// <summary>
    /// Fires when this node is selected (for upstream sync).
    /// </summary>
    public event Action<Guid>? NodeSelected;

    public LogicalTreeNodeViewModel(Guid astNodeId)
    {
        AstNodeId = astNodeId;

        _displayText = this.WhenAnyValue(x => x.TypeName, x => x.Name, x => x.ContentPreview)
            .Select(t =>
            {
                string text = string.IsNullOrEmpty(t.Item2) ? t.Item1 : $"{t.Item1} ({t.Item2})";
                if (!string.IsNullOrEmpty(t.Item3))
                {
                    string preview = t.Item3.Length > 30 ? t.Item3[..30] + "..." : t.Item3;
                    text += $" \"{preview}\"";
                }
                return text;
            })
            .ToProperty(this, x => x.DisplayText);

        this.WhenAnyValue(x => x.IsSelected)
            .Subscribe(selected =>
            {
                if (selected)
                {
                    NodeSelected?.Invoke(AstNodeId);
                }
            });
    }

    /// <summary>
    /// Finds a tree node by AST node ID recursively.
    /// </summary>
    public LogicalTreeNodeViewModel? FindByNodeId(Guid nodeId)
    {
        if (AstNodeId == nodeId)
        {
            return this;
        }

        foreach (LogicalTreeNodeViewModel child in Children)
        {
            LogicalTreeNodeViewModel? found = child.FindByNodeId(nodeId);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Selects a specific node by AST ID, deselecting all others.
    /// </summary>
    public void SelectByNodeId(Guid nodeId)
    {
        IsSelected = AstNodeId == nodeId;
        foreach (LogicalTreeNodeViewModel child in Children)
        {
            child.SelectByNodeId(nodeId);
        }
    }

    /// <summary>
    /// Collects expanded node IDs into the provided set.
    /// </summary>
    public void CollectExpandedIds(ISet<Guid> expanded)
    {
        if (IsExpanded)
        {
            expanded.Add(AstNodeId);
        }

        foreach (LogicalTreeNodeViewModel child in Children)
        {
            child.CollectExpandedIds(expanded);
        }
    }

    /// <summary>
    /// Applies expanded state from the provided set.
    /// </summary>
    public void ApplyExpandedIds(ISet<Guid> expanded)
    {
        IsExpanded = expanded.Contains(AstNodeId);
        foreach (LogicalTreeNodeViewModel child in Children)
        {
            child.ApplyExpandedIds(expanded);
        }
    }

    /// <summary>
    /// Expands all parents along the path to the specified node.
    /// </summary>
    public bool ExpandPathToNode(Guid nodeId)
    {
        if (AstNodeId == nodeId)
        {
            IsExpanded = true;
            return true;
        }

        foreach (LogicalTreeNodeViewModel child in Children)
        {
            if (child.ExpandPathToNode(nodeId))
            {
                IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a logical tree from a mutable AST document.
    /// </summary>
    public static LogicalTreeNodeViewModel? FromAstDocument(MutableAstDocument? document)
    {
        if (document?.Root is null)
        {
            return null;
        }

        return FromAstNode(document.Root);
    }

    /// <summary>
    /// Creates a logical tree node from a mutable AST object node.
    /// </summary>
    public static LogicalTreeNodeViewModel FromAstNode(MutableAstObjectNode node)
    {
        LogicalTreeNodeViewModel vm = new(node.Id)
        {
            TypeName = node.TypeName,
            Name = node.GetPropertyValue("Name") ?? node.GetPropertyValue("x:Name")
        };

        // Extract content preview
        string? contentProp = node.GetPropertyValue("Content") ?? node.GetPropertyValue("Text");
        if (contentProp is not null)
        {
            vm.ContentPreview = contentProp;
        }

        foreach (MutableAstNode child in node.Children)
        {
            if (child is MutableAstObjectNode childObj)
            {
                vm.Children.Add(FromAstNode(childObj));
            }
        }

        return vm;
    }
}
