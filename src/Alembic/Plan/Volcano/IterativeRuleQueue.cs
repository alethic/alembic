using System.Collections.Generic;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The FIFO queue used by the bottom-up search: matches are applied in the order they were discovered.
/// A match identical to one already queued (same rule, same bound nodes) is dropped, so a rule fires at
/// most once per match.
/// </summary>
public sealed class IterativeRuleQueue : RuleQueue
{

    readonly Queue<VolcanoRuleMatch> _matches = new Queue<VolcanoRuleMatch>();
    readonly HashSet<VolcanoRuleMatch> _seen = new HashSet<VolcanoRuleMatch>();

    /// <summary>
    /// Creates a queue for the given planner.
    /// </summary>
    public IterativeRuleQueue(VolcanoPlanner planner)
        : base(planner)
    {
    }

    /// <inheritdoc />
    public override void AddMatch(VolcanoRuleMatch match)
    {
        if (_seen.Add(match))
            _matches.Enqueue(match);
    }

    /// <summary>
    /// Removes and returns the next match, or <c>null</c> if the queue is empty.
    /// </summary>
    public VolcanoRuleMatch? PopMatch()
    {
        while (_matches.Count > 0)
        {
            var match = _matches.Dequeue();
            if (!SkipMatch(match))
                return match;
        }

        return null;
    }

    /// <inheritdoc />
    public override void Clear()
    {
        _matches.Clear();
        _seen.Clear();
    }

}
