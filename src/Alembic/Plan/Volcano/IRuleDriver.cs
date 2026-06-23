using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// Applies the rule matches a planner discovers, according to some search strategy.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleDriver")]
public interface IRuleDriver
{

    /// <summary>
    /// The queue of pending matches this driver consumes.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleDriver", "getRuleQueue()")]
    RuleQueue Queue { get; }

    /// <summary>
    /// Applies matches until the search is done.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleDriver", "drive()")]
    void Drive();

    /// <summary>
    /// Notifies the driver that an op has been added to a subset.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleDriver", "onProduce(RelNode, RelSubset)")]
    void OnProduce(IOpNode op, OpSubset subset);

    /// <summary>
    /// Notifies the driver that two sets have been merged.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleDriver", "onSetMerged(RelSet)")]
    void OnSetMerged(OpSet set);

    /// <summary>
    /// Resets the driver.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleDriver", "clear()")]
    void Clear();

}
