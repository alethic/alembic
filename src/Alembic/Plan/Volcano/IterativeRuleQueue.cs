using System.Collections.Generic;

using Alembic.Util;

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
        _matchList.MatchMap.Put(Planner.GetSubsetNonNull(match.Op(0)), match);
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
            {
                // The op's set may have merged since the match was queued, so its subset may have changed;
                // a stale entry is harmless and will be cleared at the end.
                var subset = Planner.GetSubset(match.Op(0));
                if (subset is not null)
                    _matchList.MatchMap.Remove(subset, match);

                return match;
            }
        }

        return null;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleQueue", "clear()")]
    public override bool Clear()
    {
        bool empty = _matchList.Queue.Count == 0 && _matchList.PreQueue.Count == 0;
        _matchList.Clear();
        return !empty;
    }

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

        // The queued matches, for fast duplicate detection. Calcite keys this on each match's digest
        // string (Set<String>); Alembic keys on the match itself via a structural comparer (same rule +
        // bound op ids), so no digest string is built per match.
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue.MatchList", "names")]
        internal readonly HashSet<VolcanoRuleMatch> Names = new HashSet<VolcanoRuleMatch>(VolcanoRuleMatch.Comparer.Instance);

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue.MatchList", "matchMap")]
        internal readonly HashMultimap<OpSubset, VolcanoRuleMatch> MatchMap = new HashMultimap<OpSubset, VolcanoRuleMatch>();

        /// <summary>
        /// Enqueues <paramref name="match"/>: substitutions go to the high-priority pre-queue, others to
        /// the main queue.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue.MatchList", "offer(VolcanoRuleMatch)")]
        internal void Offer(VolcanoRuleMatch match, bool isSubstitution)
        {
            if (isSubstitution)
                PreQueue.Enqueue(match);
            else
                Queue.Enqueue(match);
        }

        /// <summary>
        /// Empties the queues and the duplicate-detection sets.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.IterativeRuleQueue.MatchList", "clear()")]
        internal void Clear()
        {
            PreQueue.Clear();
            Queue.Clear();
            Names.Clear();
            MatchMap.Clear();
        }

    }

}
