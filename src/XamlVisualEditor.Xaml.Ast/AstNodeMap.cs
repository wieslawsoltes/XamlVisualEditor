using System.Collections.Concurrent;

namespace XamlVisualEditor.Xaml.Ast;

/// <summary>
/// Bidirectional map between AST node IDs and node instances.
/// Thread-safe for concurrent read access.
/// </summary>
public sealed class AstNodeMap
{
    private readonly ConcurrentDictionary<Guid, MutableAstNode> _map = new();

    /// <summary>
    /// Registers a node in the map.
    /// </summary>
    public void Register(MutableAstNode node)
    {
        _map[node.Id] = node;
    }

    /// <summary>
    /// Removes a node from the map.
    /// </summary>
    public bool Unregister(Guid id)
    {
        return _map.TryRemove(id, out _);
    }

    /// <summary>
    /// Looks up a node by its unique ID.
    /// </summary>
    public MutableAstNode? FindById(Guid id)
    {
        return _map.TryGetValue(id, out MutableAstNode? node) ? node : null;
    }

    /// <summary>
    /// Gets the number of registered nodes.
    /// </summary>
    public int Count => _map.Count;

    /// <summary>
    /// Clears all registered nodes.
    /// </summary>
    public void Clear()
    {
        _map.Clear();
    }

    /// <summary>
    /// Registers a node and all its descendants recursively.
    /// </summary>
    public void RegisterTree(MutableAstNode node)
    {
        Register(node);

        if (node is MutableAstObjectNode objectNode)
        {
            foreach (MutableAstPropertyNode prop in objectNode.Properties)
            {
                RegisterTree(prop);
            }

            foreach (MutableAstNode child in objectNode.Children)
            {
                RegisterTree(child);
            }
        }
        else if (node is MutableAstPropertyNode propNode && propNode.Value is not null)
        {
            RegisterTree(propNode.Value);
        }
    }
}
