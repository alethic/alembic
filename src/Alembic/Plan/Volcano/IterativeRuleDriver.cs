using System;
using System.Diagnostics;

using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// Applies matches repeatedly, in the order they were queued, until the queue is empty — the
/// exhaustive bottom-up search.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver")]
internal class IterativeRuleDriver : IRuleDriver
{

    readonly VolcanoPlanner _planner;

    readonly IterativeRuleQueue _ruleQueue;

    /// <summary>
    /// Creates a driver for the given planner.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "IterativeRuleDriver(VolcanoPlanner)")]
    public IterativeRuleDriver(VolcanoPlanner planner)
    {
        _planner = planner;
        _ruleQueue = new IterativeRuleQueue(planner);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "getRuleQueue()")]
    public RuleQueue Queue => _ruleQueue;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "drive()")]
    public void Drive()
    {
        while (true)
        {
            if (_planner.Root is null)
                throw new InvalidOperationException("OpSubset must not be null at this point");

            VolcanoRuleMatch? match = _ruleQueue.PopMatch();
            if (match is null)
                break;

            Debug.Assert(match.Rule.Matches(match));
            try
            {
                match.OnMatch();
            }
            catch (VolcanoTimeoutException)
            {
                // Planning timed out; cancel the subsequent optimization, keeping the best plan so far.
                _planner.Canonize();
                break;
            }

            // The root may have been merged with another
            // subset. Find the new root subset.
            _planner.Canonize();
        }
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "onProduce(RelNode, RelSubset)")]
    public void OnProduce(IOp op, OpSubset subset)
    {
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "onSetMerged(RelSet)")]
    public void OnSetMerged(OpSet set)
    {
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleDriver", "clear()")]
    public void Clear()
    {
        _ruleQueue.Clear();
    }

}
