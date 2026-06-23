using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// Applies matches repeatedly, in the order they were queued, until the queue is empty — the
/// exhaustive bottom-up search.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver")]
public sealed class IterativeRuleDriver : IRuleDriver
{

    readonly IterativeRuleQueue _queue;

    /// <summary>
    /// Creates a driver for the given planner.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "IterativeRuleDriver(VolcanoPlanner)")]
    public IterativeRuleDriver(VolcanoPlanner planner)
    {
        _queue = new IterativeRuleQueue(planner);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "getRuleQueue()")]
    public RuleQueue Queue => _queue;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "drive()")]
    public void Drive()
    {
        VolcanoRuleMatch? match;
        while ((match = _queue.PopMatch()) is not null)
            match.OnMatch();
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "onProduce(RelNode, RelSubset)")]
    public void OnProduce(INode node, NodeSubset subset)
    {
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "onSetMerged(RelSet)")]
    public void OnSetMerged(NodeSet set)
    {
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "clear()")]
    public void Clear()
    {
        _queue.Clear();
    }

}
