using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using ReactiveUI;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Sync;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.Collaboration;

/// <summary>
/// Represents a single collaborative XAML operation that can be sent over the wire.
/// </summary>
public sealed record XamlCollabOp(
    XamlCollabOpType Type,
    Guid NodeId,
    string? ParentNodeId,
    int ChildIndex,
    string? PropertyName,
    string? OldValue,
    string? NewValue,
    string? TypeName,
    string? XmlNamespace,
    DateTimeOffset Timestamp,
    string ParticipantId);

/// <summary>
/// Bridges AST changes to collaboration operations and vice versa.
/// Maps local AstChange records to XamlCollabOp for transmission,
/// and applies incoming XamlCollabOps to the local AST.
/// </summary>
public sealed class XamlCollabBridge : ReactiveObject, ICollaborationBridge, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private readonly AstNodeMap _nodeMap;
    private readonly SyncEngine _syncEngine;
    private string _localParticipantId = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Raised when a local change should be broadcast to remote participants.
    /// </summary>
    public IObservable<XamlCollabOp> OutgoingOps { get; }

    /// <summary>
    /// Gets or sets the local participant identifier.
    /// </summary>
    public string LocalParticipantId
    {
        get => _localParticipantId;
        set => this.RaiseAndSetIfChanged(ref _localParticipantId, value);
    }

    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <inheritdoc />
#pragma warning disable CS0067 // Event is not used yet — will be wired when transport layer is implemented
    public event Action<IReadOnlyList<AstChange>>? RemoteChangesReceived;
#pragma warning restore CS0067

    /// <inheritdoc />
    public Task SendChangesAsync(IReadOnlyList<AstChange> changes, CancellationToken ct = default)
    {
        // In a full implementation, this would serialize and transmit changes over the wire.
        // For now, convert to collab ops for the outgoing pipeline.
        return Task.CompletedTask;
    }

    public XamlCollabBridge(AstNodeMap nodeMap, SyncEngine syncEngine)
    {
        _nodeMap = nodeMap;
        _syncEngine = syncEngine;

        // Convert local AST changes to outgoing ops
        OutgoingOps = _syncEngine.SyncEvents
            .Where(e => e.Source != SyncSource.Collaboration)
            .Where(e => e.Changes is not null)
            .SelectMany(e => ConvertChangesToOps(e.Changes!));
    }

    /// <summary>
    /// Applies an incoming remote operation to the local AST.
    /// </summary>
    public void ApplyRemoteOp(XamlCollabOp op)
    {
        if (op.ParticipantId == _localParticipantId)
        {
            return; // Skip own echoed ops
        }

        ApplyOpToAst(op);
    }

    private IEnumerable<XamlCollabOp> ConvertChangesToOps(IReadOnlyList<AstChange> changes)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (AstChange change in changes)
        {
            switch (change)
            {
                case NodeAdded added:
                    yield return new XamlCollabOp(
                        XamlCollabOpType.InsertNode,
                        added.NodeId,
                        added.ParentId.ToString(),
                        added.Index,
                        null,
                        null,
                        null,
                        added.NodeTypeName,
                        null,
                        now,
                        _localParticipantId);
                    break;

                case NodeRemoved removed:
                    yield return new XamlCollabOp(
                        XamlCollabOpType.RemoveNode,
                        removed.NodeId,
                        removed.ParentId.ToString(),
                        0,
                        null,
                        null,
                        null,
                        null,
                        null,
                        now,
                        _localParticipantId);
                    break;

                case NodeMoved moved:
                    yield return new XamlCollabOp(
                        XamlCollabOpType.MoveNode,
                        moved.NodeId,
                        moved.NewParentId.ToString(),
                        moved.NewIndex,
                        null,
                        moved.OldParentId.ToString(),
                        null,
                        null,
                        null,
                        now,
                        _localParticipantId);
                    break;

                case PropertyValueChanged propChanged:
                    yield return new XamlCollabOp(
                        XamlCollabOpType.SetProperty,
                        propChanged.NodeId,
                        null,
                        0,
                        propChanged.PropertyName,
                        propChanged.OldValue,
                        propChanged.NewValue,
                        null,
                        null,
                        now,
                        _localParticipantId);
                    break;

                case TextContentChanged textChanged:
                    yield return new XamlCollabOp(
                        XamlCollabOpType.SetText,
                        textChanged.NodeId,
                        null,
                        0,
                        null,
                        textChanged.OldText,
                        textChanged.NewText,
                        null,
                        null,
                        now,
                        _localParticipantId);
                    break;
            }
        }
    }

    private void ApplyOpToAst(XamlCollabOp op)
    {
        MutableAstDocument? doc = _syncEngine.CurrentDocument;
        if (doc is null)
        {
            return;
        }

        switch (op.Type)
        {
            case XamlCollabOpType.InsertNode:
            {
                if (op.ParentNodeId is null || !Guid.TryParse(op.ParentNodeId, out Guid parentId))
                {
                    break;
                }

                MutableAstNode? parent = _nodeMap.FindById(parentId);
                if (parent is MutableAstObjectNode parentObj)
                {
                    MutableAstObjectNode newNode = new()
                    {
                        TypeName = op.TypeName ?? "UnknownType",
                        XmlNamespace = op.XmlNamespace ?? string.Empty
                    };

                    int index = Math.Min(op.ChildIndex, parentObj.Children.Count);
                    parentObj.Children.Insert(index, newNode);
                    _nodeMap.Register(newNode);
                }

                break;
            }

            case XamlCollabOpType.RemoveNode:
            {
                MutableAstNode? node = _nodeMap.FindById(op.NodeId);
                if (node?.Parent is MutableAstObjectNode parentObj)
                {
                    parentObj.Children.Remove(node);
                    _nodeMap.Unregister(node.Id);
                }

                break;
            }

            case XamlCollabOpType.MoveNode:
            {
                MutableAstNode? node = _nodeMap.FindById(op.NodeId);
                if (node is null)
                {
                    break;
                }

                if (node.Parent is MutableAstObjectNode oldParent)
                {
                    oldParent.Children.Remove(node);
                }

                if (op.ParentNodeId is not null
                    && Guid.TryParse(op.ParentNodeId, out Guid newParentId)
                    && _nodeMap.FindById(newParentId) is MutableAstObjectNode newParent)
                {
                    int index = Math.Min(op.ChildIndex, newParent.Children.Count);
                    newParent.Children.Insert(index, node);
                }

                break;
            }

            case XamlCollabOpType.SetProperty:
            {
                if (op.PropertyName is null)
                {
                    break;
                }

                MutableAstNode? node = _nodeMap.FindById(op.NodeId);
                if (node is MutableAstObjectNode objNode)
                {
                    objNode.SetPropertyValue(op.PropertyName, op.NewValue ?? string.Empty);
                }

                break;
            }

            case XamlCollabOpType.SetText:
            {
                MutableAstNode? node = _nodeMap.FindById(op.NodeId);
                if (node is MutableAstTextNode textNode)
                {
                    textNode.Text = op.NewValue ?? string.Empty;
                }

                break;
            }
        }

        // Notify sync engine of the remote change
        _syncEngine.NotifyAstChanged(doc, SyncSource.Collaboration);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
