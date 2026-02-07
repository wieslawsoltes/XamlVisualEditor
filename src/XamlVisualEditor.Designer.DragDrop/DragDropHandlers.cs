using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Designer.Core;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.Designer.DragDrop;

/// <summary>
/// Custom data format for toolbox item drag-and-drop.
/// </summary>
public static class DesignerDataFormats
{
    /// <summary>
    /// Data format for toolbox items being dragged onto the design surface.
    /// </summary>
    public static DataFormat<string> ToolboxItem { get; } = DataFormat.CreateStringApplicationFormat("ToolboxItem");
}

/// <summary>
/// Handles drop operations from the toolbox onto the design surface.
/// </summary>
public sealed class ToolboxDropHandler
{
    /// <summary>
    /// Determines whether the drag data can be dropped at the specified position.
    /// </summary>
    public bool CanDrop(IDataTransfer data, IDesignItem? targetItem, DropPosition position)
    {
        return data.Contains(DesignerDataFormats.ToolboxItem) && targetItem is not null;
    }

    /// <summary>
    /// Handles the drop operation, creating a new AST node from the toolbox item.
    /// </summary>
    public MutableAstObjectNode? Drop(
        string typeName,
        string defaultXaml,
        IDesignItem? targetItem,
        DropPosition position)
    {
        if (targetItem is not DesignItem designItem)
        {
            return null;
        }

        MutableAstObjectNode newNode = new()
        {
            TypeName = typeName,
            XmlNamespace = "https://github.com/avaloniaui"
        };

        MutableAstObjectNode parentNode = designItem.AstNode;

        switch (position)
        {
            case DropPosition.Inside:
                parentNode.Children.Add(newNode);
                break;

            case DropPosition.Before:
                if (designItem.Parent is DesignItem parentItem)
                {
                    int index = parentItem.AstNode.Children.IndexOf(designItem.AstNode);
                    if (index >= 0)
                    {
                        parentItem.AstNode.Children.Insert(index, newNode);
                    }
                }
                break;

            case DropPosition.After:
                if (designItem.Parent is DesignItem parentItem2)
                {
                    int index = parentItem2.AstNode.Children.IndexOf(designItem.AstNode);
                    if (index >= 0)
                    {
                        parentItem2.AstNode.Children.Insert(index + 1, newNode);
                    }
                }
                break;
        }

        return newNode;
    }
}

/// <summary>
/// Handles drag-and-drop operations within the design surface for rearranging elements.
/// </summary>
public sealed class SurfaceDropHandler
{
    /// <summary>
    /// Moves a design item to a new position relative to a target.
    /// </summary>
    public bool Move(IDesignItem source, IDesignItem target, DropPosition position)
    {
        if (source is not DesignItem sourceItem || target is not DesignItem targetItem)
        {
            return false;
        }

        // Prevent dropping onto self or descendant
        if (IsDescendantOf(targetItem, sourceItem))
        {
            return false;
        }

        // For Before/After, verify the target has a parent before removing source
        if (position is DropPosition.Before or DropPosition.After && targetItem.Parent is not DesignItem)
        {
            return false;
        }

        // Remove from current parent
        MutableAstNode sourceNode = sourceItem.AstNode;
        if (sourceItem.Parent is DesignItem currentParent)
        {
            currentParent.AstNode.Children.Remove(sourceNode);
            currentParent.RemoveChild(sourceItem);
        }

        // Add to new position
        switch (position)
        {
            case DropPosition.Inside:
                targetItem.AstNode.Children.Add(sourceNode);
                break;

            case DropPosition.Before:
                if (targetItem.Parent is DesignItem newParent)
                {
                    int index = newParent.AstNode.Children.IndexOf(targetItem.AstNode);
                    if (index >= 0)
                    {
                        newParent.AstNode.Children.Insert(index, sourceNode);
                    }
                }
                break;

            case DropPosition.After:
                if (targetItem.Parent is DesignItem newParent2)
                {
                    int index = newParent2.AstNode.Children.IndexOf(targetItem.AstNode);
                    if (index >= 0)
                    {
                        newParent2.AstNode.Children.Insert(index + 1, sourceNode);
                    }
                }
                break;
        }

        return true;
    }

    private static bool IsDescendantOf(IDesignItem potentialDescendant, IDesignItem ancestor)
    {
        IDesignItem? current = potentialDescendant;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
            current = current.Parent;
        }
        return false;
    }
}

/// <summary>
/// Handles drag-and-drop operations from the tree view.
/// </summary>
public sealed class TreeDropHandler
{
    /// <summary>
    /// Moves a node in the AST based on tree view drag-and-drop.
    /// </summary>
    public bool MoveNode(Guid sourceNodeId, Guid targetNodeId, DropPosition position, AstNodeMap nodeMap)
    {
        MutableAstNode? sourceNode = nodeMap.FindById(sourceNodeId);
        MutableAstNode? targetNode = nodeMap.FindById(targetNodeId);

        if (sourceNode is not MutableAstObjectNode source ||
            targetNode is not MutableAstObjectNode target)
        {
            return false;
        }

        // For Before/After positions, verify target has a valid parent before removing source
        if (position is DropPosition.Before or DropPosition.After &&
            target.Parent is not MutableAstObjectNode)
        {
            return false;
        }

        // Remove source from current parent
        if (source.Parent is MutableAstObjectNode currentParent)
        {
            currentParent.Children.Remove(source);
        }

        // Insert at new position
        switch (position)
        {
            case DropPosition.Inside:
                target.Children.Add(source);
                break;

            case DropPosition.Before:
                if (target.Parent is MutableAstObjectNode newParent)
                {
                    int index = newParent.Children.IndexOf(target);
                    if (index >= 0)
                    {
                        newParent.Children.Insert(index, source);
                    }
                }
                break;

            case DropPosition.After:
                if (target.Parent is MutableAstObjectNode newParent2)
                {
                    int index = newParent2.Children.IndexOf(target);
                    if (index >= 0)
                    {
                        newParent2.Children.Insert(index + 1, source);
                    }
                }
                break;
        }

        return true;
    }
}
