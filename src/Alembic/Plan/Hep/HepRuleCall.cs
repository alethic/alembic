using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Hep;

/// <summary>
/// The <see cref="RuleCall"/> used by <see cref="HepPlanner"/>. A rule may register several equivalents
/// per match; heuristic rewriting applies the first that differs from the matched node.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRuleCall")]
public sealed class HepRuleCall : RuleCall
{

    readonly List<INode> _results = new List<INode>();

    /// <summary>
    /// Creates a call seeded at <paramref name="operand0"/> over the operand-bound nodes.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRuleCall", "HepRuleCall(RelOptPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>, List<RelNode>)")]
    public HepRuleCall(IPlanner planner, RuleOperand operand0, ImmutableArray<INode> nodes)
        : base(planner, operand0, nodes)
    {

    }

    /// <summary>
    /// The equivalents the rule registered, in the order it registered them.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRuleCall", "getResults()")]
    public IReadOnlyList<INode> Results => _results;

    /// <inheritdoc />
    /// <remarks>
    /// The heuristic planner keeps a single best plan, so it records only the primary equivalent; the
    /// secondary <paramref name="equiv"/> map (used by the cost-based planner to explore alternatives)
    /// has no role here and is ignored.
    /// </remarks>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRuleCall", "transformTo(RelNode, Map<RelNode, RelNode>, RelHintsPropagator)")]
    public override void TransformTo(INode equivalent, IReadOnlyDictionary<INode, INode> equiv)
    {
        _results.Add(equivalent);
    }

}
