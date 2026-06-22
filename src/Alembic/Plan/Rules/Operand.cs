using System;
using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// A node-tree pattern: a predicate over a node, and optional child operands. When no child operands
/// are given, the operand matches regardless of the node's children; otherwise the children must
/// match positionally. Every <see cref="IRule"/> has one; matching is performed by
/// <see cref="OperandMatcher"/>.
/// </summary>
public sealed class Operand
{

    /// <summary>
    /// Creates an operand from a predicate and optional child operands.
    /// </summary>
    public Operand(Func<INode, bool> predicate, params Operand[] children)
    {
        Predicate = predicate;
        Children = children.ToImmutableArray();
    }

    /// <summary>
    /// The predicate this operand applies to a node (type and/or value tests).
    /// </summary>
    public Func<INode, bool> Predicate { get; }

    /// <summary>
    /// The child operands, or empty to match any children.
    /// </summary>
    public ImmutableArray<Operand> Children { get; }

    /// <summary>
    /// An operand matching nodes of type <typeparamref name="TNode"/>, with the given child operands.
    /// </summary>
    public static Operand Of<TNode>(params Operand[] children)
        where TNode : INode
    {
        return new Operand(static node => node is TNode, children);
    }

    /// <summary>
    /// An operand matching nodes of type <typeparamref name="TNode"/> that also satisfy a predicate.
    /// </summary>
    public static Operand Of<TNode>(Func<TNode, bool> predicate, params Operand[] children)
        where TNode : INode
    {
        return new Operand(node => node is TNode match && predicate(match), children);
    }

}
