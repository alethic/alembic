using System.Collections.Generic;

namespace Alembic.Plan.Volcano;

/// <summary>
/// Holds the rule matches the planner has discovered but not yet applied. A match identical to one
/// already queued (same rule, same bound nodes) is dropped, so a rule fires at most once per match.
/// </summary>
public sealed class RuleQueue
{

    readonly Queue<VolcanoRuleMatch> _matches = new Queue<VolcanoRuleMatch>();
    readonly HashSet<VolcanoRuleMatch> _seen = new HashSet<VolcanoRuleMatch>();

    /// <summary>
    /// Adds a match unless an identical one has already been queued.
    /// </summary>
    public void AddMatch(VolcanoRuleMatch match)
    {
        if (_seen.Add(match))
            _matches.Enqueue(match);
    }

    /// <summary>
    /// Removes and returns the next match, or <c>null</c> if the queue is empty.
    /// </summary>
    public VolcanoRuleMatch? PopMatch()
    {
        return _matches.Count > 0 ? _matches.Dequeue() : null;
    }

    /// <summary>
    /// Empties the queue.
    /// </summary>
    public void Clear()
    {
        _matches.Clear();
        _seen.Clear();
    }

}
