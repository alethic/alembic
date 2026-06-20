using System;
using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// A node-tree pattern: a node type, an optional predicate, and optional child operands.
/// When no child operands are given, the operand matches a node of its type regardless of that
/// node's children; otherwise the children must match positionally.
/// </summary>
public sealed class Operand
{

    readonly Type _nodeType;
    readonly Func<INode, bool>? _predicate;
    readonly ImmutableArray<Operand> _children;

    Operand(Type nodeType, Func<INode, bool>? predicate, ImmutableArray<Operand> children)
    {
        _nodeType = nodeType;
        _predicate = predicate;
        _children = children;
    }

    /// <summary>
    /// The node type this operand matches.
    /// </summary>
    public Type NodeType => _nodeType;

    /// <summary>
    /// The child operands, or empty to match any children.
    /// </summary>
    public ImmutableArray<Operand> Children => _children;

    /// <summary>
    /// Whether this operand matches the given node (type and predicate only, not children).
    /// </summary>
    public bool Matches(INode node)
    {
        if (!_nodeType.IsInstanceOfType(node)) return false;
        if (_predicate is not null && !_predicate(node)) return false;

        return true;
    }

    /// <summary>
    /// An operand matching <typeparamref name="TNode"/> with the given child operands.
    /// </summary>
    public static Operand Of<TNode>(params Operand[] children)
        where TNode : INode
    {
        return new Operand(typeof(TNode), null, children.ToImmutableArray());
    }

    /// <summary>
    /// An operand matching <typeparamref name="TNode"/> that also satisfies a predicate.
    /// </summary>
    public static Operand Of<TNode>(Func<TNode, bool> predicate, params Operand[] children)
        where TNode : INode
    {
        return new Operand(typeof(TNode), node => predicate((TNode)node), children.ToImmutableArray());
    }

}
