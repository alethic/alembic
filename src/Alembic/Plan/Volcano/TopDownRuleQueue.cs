using System;
using System.Collections.Generic;

using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The rule queue for the top-down search. Matches are bucketed by the op they are rooted at, so the
/// driver can pull only the matches relevant to the op it is currently optimizing, optionally filtered
/// (e.g. to transformation rules while merely exploring).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleQueue")]
internal class TopDownRuleQueue : RuleQueue
{

    readonly Dictionary<IOp, LinkedList<VolcanoRuleMatch>> _matches = new Dictionary<IOp, LinkedList<VolcanoRuleMatch>>(ReferenceEqualityComparer.Instance);

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleQueue", "names")]
    readonly HashSet<string> _names = new HashSet<string>();

    /// <summary>
    /// Creates a queue for the given planner.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleQueue", "TopDownRuleQueue(VolcanoPlanner)")]
    public TopDownRuleQueue(VolcanoPlanner planner)
        : base(planner)
    {
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleQueue", "addMatch(VolcanoRuleMatch)")]
    internal override void AddMatch(VolcanoRuleMatch match)
    {
        var rel = match.Op(0);
        if (!_matches.TryGetValue(rel, out var queue))
        {
            queue = new LinkedList<VolcanoRuleMatch>();
            _matches[rel] = queue;
        }

        if (!_names.Add(match.ToString()))
            return;

        // A substitution rule is applied first even though queued last: the driver pops from the front
        // and pushes onto a stack, so a front-queued non-substitution rule ends up under the
        // back-queued substitution rule on the stack and runs after it.
        if (!Planner.IsSubstituteRule(match))
            queue.AddFirst(match);
        else
            queue.AddLast(match);
    }

    /// <summary>
    /// Removes and returns the next match rooted at <paramref name="rel"/> that satisfies
    /// <paramref name="predicate"/> (if any), or <c>null</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleQueue", "popMatch(Pair<RelNode, Predicate<VolcanoRuleMatch>>)")]
    public VolcanoRuleMatch? PopMatch(IOp rel, Func<VolcanoRuleMatch, bool>? predicate)
    {
        if (!_matches.TryGetValue(rel, out var queue))
            return null;

        var op = queue.First;
        while (op is not null)
        {
            var next = op.Next;
            var match = op.Value;
            if (predicate is not null && !predicate(match))
            {
                op = next;
                continue;
            }

            queue.Remove(op);
            if (!SkipMatch(match))
                return match;

            op = next;
        }

        return null;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleQueue", "clear()")]
    public override bool Clear()
    {
        bool empty = _matches.Count == 0;
        _matches.Clear();
        _names.Clear();
        return !empty;
    }

}
