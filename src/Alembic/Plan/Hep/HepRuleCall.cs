using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Hep;

/// <summary>
/// The <see cref="OpRuleCall"/> used by <see cref="HepPlanner"/>. A rule may register several equivalents
/// per match; heuristic rewriting applies the first that differs from the matched op.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRuleCall")]
public sealed class HepRuleCall : OpRuleCall
{

    readonly List<IOpNode> _results = new List<IOpNode>();

    /// <summary>
    /// Creates a call seeded at <paramref name="operand0"/> over the operand-bound ops.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRuleCall", "HepRuleCall(RelOptPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>, List<RelNode>)")]
    public HepRuleCall(IOpPlanner planner, OpRuleOperand operand0, ImmutableArray<IOpNode> ops)
        : base(planner, operand0, ops)
    {

    }

    /// <summary>
    /// The equivalents the rule registered, in the order it registered them.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRuleCall", "getResults()")]
    public IReadOnlyList<IOpNode> Results => _results;

    /// <inheritdoc />
    /// <remarks>
    /// The heuristic planner keeps a single best plan, so it records only the primary equivalent; the
    /// secondary <paramref name="equiv"/> map (used by the cost-based planner to explore alternatives)
    /// has no role here and is ignored.
    /// </remarks>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRuleCall", "transformTo(RelNode, Map<RelNode, RelNode>, RelHintsPropagator)")]
    public override void TransformTo(IOpNode equivalent, IReadOnlyDictionary<IOpNode, IOpNode> equiv)
    {
        _results.Add(equivalent);
    }

}
