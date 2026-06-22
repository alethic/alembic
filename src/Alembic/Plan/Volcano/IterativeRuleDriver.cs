using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// Applies matches repeatedly, in the order they were queued, until the queue is empty — the
/// exhaustive bottom-up search.
/// </summary>
public sealed class IterativeRuleDriver : IRuleDriver
{

    readonly IterativeRuleQueue _queue;

    /// <summary>
    /// Creates a driver for the given planner.
    /// </summary>
    public IterativeRuleDriver(VolcanoPlanner planner)
    {
        _queue = new IterativeRuleQueue(planner);
    }

    /// <inheritdoc />
    public RuleQueue Queue => _queue;

    /// <inheritdoc />
    public void Drive()
    {
        VolcanoRuleMatch? match;
        while ((match = _queue.PopMatch()) is not null)
            match.OnMatch();
    }

    /// <inheritdoc />
    public void OnProduce(INode node, NodeSubset subset)
    {
    }

    /// <inheritdoc />
    public void OnSetMerged(NodeSet set)
    {
    }

    /// <inheritdoc />
    public void Clear()
    {
        _queue.Clear();
    }

}
