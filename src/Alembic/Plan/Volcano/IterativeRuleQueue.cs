using System.Collections.Generic;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The match list used by the bottom-up search: matches are applied in the order they were discovered
/// (FIFO), except that substitution-rule matches jump ahead via a pre-queue. A match identical to one
/// already queued (same rule, same bound ops) is dropped, so a rule fires at most once per match.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue")]
internal class IterativeRuleQueue : RuleQueue
{

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue", "matchList")]
    readonly MatchList _matchList = new MatchList();

    /// <summary>
    /// Creates a queue for the given planner.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue", "IterativeRuleQueue(VolcanoPlanner)")]
    public IterativeRuleQueue(VolcanoPlanner planner)
        : base(planner)
    {
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue", "addMatch(VolcanoRuleMatch)")]
    internal override void AddMatch(VolcanoRuleMatch match)
    {
        if (!_matchList.Names.Add(match))
            return;

        _matchList.Offer(match, Planner.IsSubstituteRule(match));
    }

    /// <summary>
    /// Removes and returns the next match — taking from the substitution pre-queue first, then the main
    /// queue — skipping any that have gone stale, or <c>null</c> if the list is empty.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue", "popMatch()")]
    public VolcanoRuleMatch? PopMatch()
    {
        while (_matchList.PreQueue.Count > 0 || _matchList.Queue.Count > 0)
        {
            var match = _matchList.PreQueue.Count > 0
                ? _matchList.PreQueue.Dequeue()
                : _matchList.Queue.Dequeue();

            if (!SkipMatch(match))
                return match;
        }

        return null;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue", "clear()")]
    public override void Clear() => _matchList.Clear();

    /// <summary>
    /// A set of waiting rule-matches: the main FIFO <see cref="Queue"/>, the <see cref="PreQueue"/> that
    /// gives substitution rules priority, and a dedup set.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue.MatchList")]
    sealed class MatchList
    {

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue.MatchList", "preQueue")]
        internal readonly Queue<VolcanoRuleMatch> PreQueue = new Queue<VolcanoRuleMatch>();

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue.MatchList", "queue")]
        internal readonly Queue<VolcanoRuleMatch> Queue = new Queue<VolcanoRuleMatch>();

        // Calcite's `names` is a Set<String> of match.toString() digests, which embed each op's
        // RelNode.getId(). Alembic doesn't port getId(), so it dedups by match identity instead
        // (VolcanoRuleMatch equality — same rule, same bound ops by reference); equivalent, since both
        // are per-instance.
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue.MatchList", "names")]
        internal readonly HashSet<VolcanoRuleMatch> Names = new HashSet<VolcanoRuleMatch>();

        // Calcite's `matchMap` (Multimap<RelSubset, VolcanoRuleMatch>) is not ported: it is put to and
        // removed from but never read — vestigial bookkeeping.

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue.MatchList", "offer(VolcanoRuleMatch)")]
        internal void Offer(VolcanoRuleMatch match, bool isSubstitution)
        {
            if (isSubstitution)
                PreQueue.Enqueue(match);
            else
                Queue.Enqueue(match);
        }

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue.MatchList", "clear()")]
        internal void Clear()
        {
            PreQueue.Clear();
            Queue.Clear();
            Names.Clear();
        }

    }

}
