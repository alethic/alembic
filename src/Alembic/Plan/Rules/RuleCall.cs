using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// The context of a single rule match: the nodes bound to each operand and <see cref="Transform"/> —
/// the sink through which the rule registers an equivalent. This base is planner-agnostic; each
/// planner provides a subclass that decides what <see cref="Transform"/> does.
/// </summary>
/// <remarks>
/// A rule reaches its matched nodes through <see cref="Node"/>, not by navigating
/// <see cref="INode.Children"/>: under a heuristic planner the children are the concrete nodes, but
/// under a cost-based planner they are equivalence subsets, so only the operand-bound nodes are
/// guaranteed to be the concrete types the rule expects.
/// </remarks>
public abstract class RuleCall
{

    /// <summary>
    /// Creates a call over the nodes bound to the rule's operands, in operand order (the operand root
    /// first, then a pre-order walk of the child operands).
    /// </summary>
    protected RuleCall(ImmutableArray<INode> nodes)
    {
        Nodes = nodes;
    }

    /// <summary>
    /// The nodes bound to the rule's operands, in operand order.
    /// </summary>
    public ImmutableArray<INode> Nodes { get; }

    /// <summary>
    /// The node bound to the operand at the given ordinal. The operand root is ordinal 0.
    /// </summary>
    public INode Node(int ordinal) => Nodes[ordinal];

    /// <summary>
    /// Registers an equivalent for the matched node. What this does is planner-specific.
    /// </summary>
    public abstract void Transform(INode equivalent);

}
