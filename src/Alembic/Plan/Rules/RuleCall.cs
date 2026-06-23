using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// The context of a single rule match: the ops bound to each operand and <see cref="TransformTo(IOpNode)"/> —
/// the sink through which the rule registers an equivalent. This base is planner-agnostic; each
/// planner provides a subclass that decides what <see cref="TransformTo(IOpNode)"/> does.
/// </summary>
/// <remarks>
/// A rule reaches its matched ops through <see cref="Op"/>, not by navigating
/// <see cref="IOpNode.Children"/>: under a heuristic planner the children are the concrete ops, but
/// under a cost-based planner they are equivalence subsets, so only the operand-bound ops are
/// guaranteed to be the concrete types the rule expects.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall")]
public abstract class RuleCall
{

    static int _nextId;

    /// <summary>
    /// Creates a call seeded at <paramref name="operand0"/>, over the ops bound to the rule's operands
    /// in operand order (the operand root first, then a pre-order walk of the child operands). The rule
    /// is taken from the seed operand.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "RelOptRuleCall(RelOptPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>)")]
    protected RuleCall(IPlanner planner, RuleOperand operand0, ImmutableArray<IOpNode> ops)
    {
        Id = _nextId++;
        Planner = planner;
        Operand0 = operand0;
        Rule = operand0.Rule;
        Ops = ops;
    }

    /// <summary>
    /// This call's stable identity, assigned in creation order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "id")]
    public int Id { get; }

    /// <summary>
    /// The planner that issued this call.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "getPlanner()")]
    public IPlanner Planner { get; }

    /// <summary>
    /// The operand the match is seeded from (the operand bound to the op that started the match). The
    /// rule is reached through it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "getOperand0()")]
    public RuleOperand Operand0 { get; }

    /// <summary>
    /// The rule this call is for.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "getRule()")]
    public Rule Rule { get; }

    /// <summary>
    /// The ops bound to the rule's operands, in operand order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "getRelList()")]
    public ImmutableArray<IOpNode> Ops { get; }

    /// <summary>
    /// The op bound to the operand at the given ordinal. The operand root is ordinal 0.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "rel(int)")]
    public IOpNode Op(int ordinal) => Ops[ordinal];

    /// <summary>
    /// Registers an equivalent for the matched op, with no other equivalences.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "transformTo(RelNode)")]
    public void TransformTo(IOpNode equivalent) => TransformTo(equivalent, ImmutableDictionary<IOpNode, IOpNode>.Empty);

    /// <summary>
    /// Registers an equivalent for the matched op, along with a map of other equivalences to register
    /// first (each key is registered as equivalent to its value, so the root registration below does not
    /// register them twice). What this does is planner-specific.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "transformTo(RelNode, Map<RelNode, RelNode>)")]
    public abstract void TransformTo(IOpNode equivalent, IReadOnlyDictionary<IOpNode, IOpNode> equiv);

}
