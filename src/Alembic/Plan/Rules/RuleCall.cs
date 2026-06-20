using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// The context of a single rule match: the nodes bound by the operand pattern (in pre-order, so
/// node 0 is the matched root) and a sink for the equivalent the rule produces.
/// </summary>
public sealed class RuleCall
{

    readonly ImmutableArray<INode> _nodes;

    /// <summary>
    /// Creates a call over the nodes bound by the match.
    /// </summary>
    public RuleCall(ImmutableArray<INode> nodes)
    {
        _nodes = nodes;
    }

    /// <summary>
    /// The bound nodes, in operand pre-order.
    /// </summary>
    public ImmutableArray<INode> Nodes => _nodes;

    /// <summary>
    /// The equivalent registered by the rule, if any.
    /// </summary>
    public INode? Result { get; private set; }

    /// <summary>
    /// The bound node at the given ordinal.
    /// </summary>
    public INode Node(int ordinal)
    {
        return _nodes[ordinal];
    }

    /// <summary>
    /// The bound node at the given ordinal, typed.
    /// </summary>
    public TNode Node<TNode>(int ordinal)
        where TNode : INode
    {
        return (TNode)_nodes[ordinal];
    }

    /// <summary>
    /// Registers an equivalent for the matched subtree.
    /// </summary>
    public void Transform(INode equivalent)
    {
        Result = equivalent;
    }

}
