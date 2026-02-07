namespace XamlVisualEditor.Xaml.Ast;

/// <summary>
/// Visitor pattern interface for traversing the mutable AST.
/// </summary>
public interface IAstVisitor
{
    /// <summary>Visits an object node (element) before its children.</summary>
    void VisitObjectNode(MutableAstObjectNode node);

    /// <summary>Called after visiting all children of an object node.</summary>
    void EndVisitObjectNode(MutableAstObjectNode node);

    /// <summary>Visits a property node before its value.</summary>
    void VisitPropertyNode(MutableAstPropertyNode node);

    /// <summary>Called after visiting the value of a property node.</summary>
    void EndVisitPropertyNode(MutableAstPropertyNode node);

    /// <summary>Visits a text content node.</summary>
    void VisitTextNode(MutableAstTextNode node);

    /// <summary>Visits a directive node.</summary>
    void VisitDirectiveNode(MutableAstDirectiveNode node);
}

/// <summary>
/// Transformer pattern interface for modifying the mutable AST.
/// Returns the (potentially replaced) node.
/// </summary>
public interface IAstTransformer
{
    /// <summary>Transforms an object node, returning it or a replacement.</summary>
    MutableAstNode TransformObjectNode(MutableAstObjectNode node);

    /// <summary>Transforms a property node, returning it or a replacement.</summary>
    MutableAstNode TransformPropertyNode(MutableAstPropertyNode node);

    /// <summary>Transforms a text node, returning it or a replacement.</summary>
    MutableAstNode TransformTextNode(MutableAstTextNode node);

    /// <summary>Transforms a directive node, returning it or a replacement.</summary>
    MutableAstNode TransformDirectiveNode(MutableAstDirectiveNode node);
}

/// <summary>
/// Base class for AST visitors with default no-op implementations.
/// </summary>
public abstract class AstVisitorBase : IAstVisitor
{
    /// <inheritdoc />
    public virtual void VisitObjectNode(MutableAstObjectNode node) { }

    /// <inheritdoc />
    public virtual void EndVisitObjectNode(MutableAstObjectNode node) { }

    /// <inheritdoc />
    public virtual void VisitPropertyNode(MutableAstPropertyNode node) { }

    /// <inheritdoc />
    public virtual void EndVisitPropertyNode(MutableAstPropertyNode node) { }

    /// <inheritdoc />
    public virtual void VisitTextNode(MutableAstTextNode node) { }

    /// <inheritdoc />
    public virtual void VisitDirectiveNode(MutableAstDirectiveNode node) { }
}

/// <summary>
/// Base class for AST transformers with default pass-through implementations.
/// </summary>
public abstract class AstTransformerBase : IAstTransformer
{
    /// <inheritdoc />
    public virtual MutableAstNode TransformObjectNode(MutableAstObjectNode node) => node;

    /// <inheritdoc />
    public virtual MutableAstNode TransformPropertyNode(MutableAstPropertyNode node) => node;

    /// <inheritdoc />
    public virtual MutableAstNode TransformTextNode(MutableAstTextNode node) => node;

    /// <inheritdoc />
    public virtual MutableAstNode TransformDirectiveNode(MutableAstDirectiveNode node) => node;
}
