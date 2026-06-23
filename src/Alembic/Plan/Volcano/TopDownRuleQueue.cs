using System;
using System.Collections.Generic;

using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The rule queue for the top-down search. Matches are bucketed by the node they are rooted at, so the
/// driver can pull only the matches relevant to the node it is currently optimizing, optionally filtered
/// (e.g. to transformation rules while merely exploring).
/// </summary>
[Provenance("org.apache.calcite.plan.volcano.TopDownRuleQueue")]
public sealed class TopDownRuleQueue : RuleQueue
{

    [Provenance("org.apache.calcite.plan.volcano.TopDownRuleQueue", "matches")]
    readonly Dictionary<INode, LinkedList<VolcanoRuleMatch>> _matches = new Dictionary<INode, LinkedList<VolcanoRuleMatch>>(ReferenceEqualityComparer.Instance);
    readonly HashSet<VolcanoRuleMatch> _seen = new HashSet<VolcanoRuleMatch>();

    /// <summary>
    /// Creates a queue for the given planner.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.TopDownRuleQueue", "TopDownRuleQueue(VolcanoPlanner)")]
    public TopDownRuleQueue(VolcanoPlanner planner)
        : base(planner)
    {
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.TopDownRuleQueue", "addMatch(VolcanoRuleMatch)")]
    public override void AddMatch(VolcanoRuleMatch match)
    {
        var rel = match.Node(0);
        if (!_matches.TryGetValue(rel, out var queue))
        {
            queue = new LinkedList<VolcanoRuleMatch>();
            _matches[rel] = queue;
        }

        if (!_seen.Add(match))
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
    [Provenance("org.apache.calcite.plan.volcano.TopDownRuleQueue", "popMatch(Pair<RelNode, Predicate<VolcanoRuleMatch>>)")]
    public VolcanoRuleMatch? PopMatch(INode rel, Func<VolcanoRuleMatch, bool>? predicate)
    {
        if (!_matches.TryGetValue(rel, out var queue))
            return null;

        var node = queue.First;
        while (node is not null)
        {
            var next = node.Next;
            var match = node.Value;
            if (predicate is not null && !predicate(match))
            {
                node = next;
                continue;
            }

            queue.Remove(node);
            if (!SkipMatch(match))
                return match;

            node = next;
        }

        return null;
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.TopDownRuleQueue", "clear()")]
    public override void Clear()
    {
        _matches.Clear();
        _seen.Clear();
    }

}
