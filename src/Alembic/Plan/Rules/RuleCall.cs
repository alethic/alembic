using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// The context of a single rule match: the nodes bound to each operand and <see cref="TransformTo(INode)"/> —
/// the sink through which the rule registers an equivalent. This base is planner-agnostic; each
/// planner provides a subclass that decides what <see cref="TransformTo(INode)"/> does.
/// </summary>
/// <remarks>
/// A rule reaches its matched nodes through <see cref="Node"/>, not by navigating
/// <see cref="INode.Children"/>: under a heuristic planner the children are the concrete nodes, but
/// under a cost-based planner they are equivalence subsets, so only the operand-bound nodes are
/// guaranteed to be the concrete types the rule expects.
/// </remarks>
[Provenance("org.apache.calcite.plan.RelOptRuleCall")]
public abstract class RuleCall
{

    static int _nextId;

    /// <summary>
    /// Creates a call seeded at <paramref name="operand0"/>, over the nodes bound to the rule's operands
    /// in operand order (the operand root first, then a pre-order walk of the child operands). The rule
    /// is taken from the seed operand.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptRuleCall", "RelOptRuleCall(RelOptPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>)")]
    protected RuleCall(IPlanner planner, RuleOperand operand0, ImmutableArray<INode> nodes)
    {
        Id = _nextId++;
        Planner = planner;
        Operand0 = operand0;
        Rule = operand0.Rule;
        Nodes = nodes;
    }

    /// <summary>
    /// This call's stable identity, assigned in creation order.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptRuleCall", "id")]
    public int Id { get; }

    /// <summary>
    /// The planner that issued this call.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptRuleCall", "getPlanner()")]
    public IPlanner Planner { get; }

    /// <summary>
    /// The operand the match is seeded from (the operand bound to the node that started the match). The
    /// rule is reached through it.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptRuleCall", "getOperand0()")]
    public RuleOperand Operand0 { get; }

    /// <summary>
    /// The rule this call is for.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptRuleCall", "getRule()")]
    public Rule Rule { get; }

    /// <summary>
    /// The nodes bound to the rule's operands, in operand order.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptRuleCall", "getRelList()")]
    public ImmutableArray<INode> Nodes { get; }

    /// <summary>
    /// The node bound to the operand at the given ordinal. The operand root is ordinal 0.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptRuleCall", "rel(int)")]
    public INode Node(int ordinal) => Nodes[ordinal];

    /// <summary>
    /// Registers an equivalent for the matched node, with no other equivalences.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptRuleCall", "transformTo(RelNode)")]
    public void TransformTo(INode equivalent) => TransformTo(equivalent, ImmutableDictionary<INode, INode>.Empty);

    /// <summary>
    /// Registers an equivalent for the matched node, along with a map of other equivalences to register
    /// first (each key is registered as equivalent to its value, so the root registration below does not
    /// register them twice). What this does is planner-specific.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptRuleCall", "transformTo(RelNode, Map<RelNode, RelNode>)")]
    public abstract void TransformTo(INode equivalent, IReadOnlyDictionary<INode, INode> equiv);

}
