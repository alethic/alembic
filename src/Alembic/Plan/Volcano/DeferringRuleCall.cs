using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The rule call used while firing rules during registration. Instead of applying the rule
/// immediately, each completed match is deferred by adding a <see cref="VolcanoRuleMatch"/> to the
/// planner's rule queue, for the <see cref="IRuleDriver"/> to apply later.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner.DeferringRuleCall")]
internal class DeferringRuleCall : VolcanoRuleCall
{

    readonly VolcanoPlanner _planner;

    /// <summary>
    /// Creates a deferring call for <paramref name="planner"/> rooted at <paramref name="operand0"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner.DeferringRuleCall", "DeferringRuleCall(VolcanoPlanner, RelOptRuleOperand)")]
    internal DeferringRuleCall(VolcanoPlanner planner, OpRuleOperand operand0)
        : base(planner, operand0)
    {
        _planner = planner;
    }

    /// <summary>
    /// Queues the completed match rather than applying it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner.DeferringRuleCall", "onMatch()")]
    public override void OnMatch()
    {
        _planner.RuleDriver.Queue.AddMatch(new VolcanoRuleMatch(_planner, Operand0, Ops, NodeInputs));
    }

}
