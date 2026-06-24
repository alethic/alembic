using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

using Alembic.Algebra;
using Alembic.Algebra.Metadata;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The members of a <see cref="OpSet"/> that share one trait set — equivalent plans with identical
/// physical properties. A subset is itself an <see cref="IOp"/> so it can stand in as a child of a
/// registered op, and it remembers the cheapest member found so far.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset")]
public class OpSubset : AbstractOp
{

    /// <summary>
    /// Creates the subset of <paramref name="set"/> for <paramref name="traits"/> and computes its initial
    /// best cost.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "RelSubset(RelOptCluster, RelSet, RelTraitSet)")]
    internal OpSubset(OpCluster cluster, OpSet set, OpTraitSet traits)
        : base(cluster, traits)
    {
        Set = set;
        Debug.Assert(traits.AllSimple());
        ComputeBestCost((VolcanoPlanner)cluster.Planner);
        UpperBound = BestCost;
    }

    /// <summary>
    /// Seeds <see cref="Best"/> / <see cref="BestCost"/> by scanning the members already present in this
    /// subset (the set may already hold ops whose traits satisfy this subset's), picking the cheapest.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "computeBestCost(RelOptPlanner)")]
    void ComputeBestCost(VolcanoPlanner planner)
    {
        BestCost = planner.CostFactory.MakeInfiniteCost();
        var mq = Cluster.GetMetadataQuery();
        foreach (var rel in GetRels())
        {
            var cost = planner.GetCost(rel, mq);
            if (cost.IsLessThan(BestCost))
            {
                BestCost = cost;
                Best = rel;
            }
        }
    }

    /// <summary>
    /// Where a subset is in its top-down optimization.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset.OptimizeState")]
    public enum OptimizeState
    {

        /// <summary>
        /// Optimization has started but no winner is known yet.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset.OptimizeState", "OPTIMIZING")]
        Optimizing,

        /// <summary>
        /// Optimization has finished (with or without a winner).
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset.OptimizeState", "COMPLETED")]
        Completed

    }

    /// <summary>
    /// The equivalence set this subset belongs to.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getSet()")]
    internal OpSet Set { get; }

    /// <summary>
    /// The cheapest member found so far, or <c>null</c> if none has a finite cost yet.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getBest()")]
    public IOp? Best { get; internal set; }

    /// <summary>
    /// The cost of <see cref="Best"/> (infinite until a member is costed).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "bestCost")]
    internal IOpCost BestCost;

    /// <summary>
    /// Adds <paramref name="op"/> as an equivalent expression in this subset's set: notifies listeners
    /// that an equivalent has been found, then appends it via <see cref="OpSet.AddInternal"/>. An op
    /// already in the set is ignored.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "add(RelNode)")]
    internal void Add(IOp op)
    {
        if (Set.Ops.Contains(op))
            return;

        ((AbstractOpPlanner)op.Cluster.Planner).FireOpEquivalenceFound(op, Set.Id, !op.Convention!.Equals(IConvention.None));
        Set.AddInternal(op);
    }

    // ~ Top-down (Cascades) optimization state ---------------------------------

    /// <summary>
    /// This subset's optimization task state, or <c>null</c> if it has not been optimized.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "taskState")]
    internal OptimizeState? TaskState;

    /// <summary>
    /// The upper bound from the last optimize call (a winner must cost no more than this).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "upperBound")]
    internal IOpCost UpperBound = null!;

    bool _delivered;
    bool _required;
    bool _enforceDisabled;

    /// <summary>
    /// Whether this subset should trigger rules now that it has become delivered.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "triggerRule")]
    internal bool TriggerRule;

    HashSet<IOp>? _passThroughCache;

    /// <summary>
    /// Whether some member of this subset delivers its trait set.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "isDelivered()")]
    public bool IsDelivered => _delivered;

    /// <summary>
    /// Whether a parent requires this subset's trait set.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "isRequired()")]
    public bool IsRequired => _required;

    /// <summary>
    /// Marks this subset's traits as delivered, arming the rule trigger if it was not already delivered.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "setDelivered()")]
    internal void SetDelivered()
    {
        TriggerRule = !_delivered;
        _delivered = true;
    }

    /// <summary>
    /// Marks this subset's traits as required by a parent.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "setRequired()")]
    internal void SetRequired()
    {
        TriggerRule = false;
        _required = true;
    }

    /// <summary>
    /// Disables enforcer (converter) generation for this subset.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "disableEnforcing()")]
    internal void DisableEnforcing() => _enforceDisabled = true;

    /// <summary>
    /// Whether enforcer generation is disabled for this subset.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "isEnforceDisabled()")]
    internal bool IsEnforceDisabled => _enforceDisabled;

    /// <summary>
    /// The best cost if this subset is fully optimized and its winner is within the upper bound,
    /// otherwise <c>null</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getWinnerCost()")]
    internal IOpCost? GetWinnerCost()
    {
        if (TaskState == OptimizeState.Completed && BestCost.IsLessThanOrEqual(UpperBound))
            return BestCost;

        return null;
    }

    /// <summary>
    /// Begins optimizing this subset under <paramref name="upperBound"/>, tightening the bound toward the
    /// best known cost.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "startOptimize(RelOptCost)")]
    internal void StartOptimize(IOpCost upperBound)
    {
        if (UpperBound.IsLessThan(upperBound))
        {
            UpperBound = upperBound;
            if (BestCost.IsLessThan(UpperBound))
                UpperBound = BestCost;
        }

        TaskState = OptimizeState.Optimizing;
    }

    /// <summary>
    /// Marks this subset's optimization as complete.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "setOptimized()")]
    internal void SetOptimized() => TaskState = OptimizeState.Completed;

    /// <summary>
    /// Clears the optimization task state and resets the upper bound; returns whether it had been optimized.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "resetTaskState()")]
    internal bool ResetTaskState()
    {
        bool optimized = TaskState is not null;
        TaskState = null;
        UpperBound = BestCost;
        return optimized;
    }

    /// <summary>
    /// Asks <paramref name="op"/> (a physical op) to pass this subset's required traits down to its
    /// inputs, returning the delivering op, or <c>null</c> (also if it has already been asked).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "passThrough(RelNode)")]
    internal IOp? PassThrough(IOp op)
    {
        if (op is not IPhysicalOp physical)
            return null;

        _passThroughCache ??= new HashSet<IOp>(ReferenceEqualityComparer.Instance);
        if (!_passThroughCache.Add(op))
            return null;

        return physical.PassThrough(Traits);
    }

    /// <summary>
    /// Folds <paramref name="other"/>'s pass-through cache into this subset's during a set merge: adopt
    /// it wholesale when this subset has none, otherwise union the entries in. Mirrors the
    /// <c>passThroughCache</c> merge in Calcite's <c>RelSet.mergeWith</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "passThroughCache")]
    internal void AdoptPassThroughCache(OpSubset other)
    {
        if (_passThroughCache is null)
            _passThroughCache = other._passThroughCache;
        else if (other._passThroughCache is not null)
            _passThroughCache.UnionWith(other._passThroughCache);
    }

    /// <summary>
    /// Whether this subset's set has been fully explored.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "isExplored()")]
    internal bool IsExplored => Set.Exploring == OpSet.ExploringState.Explored;

    /// <summary>
    /// Begins exploring this subset's set; returns <c>false</c> if exploration is already underway or done.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "explore()")]
    internal bool Explore()
    {
        if (Set.Exploring is not null)
            return false;

        Set.Exploring = OpSet.ExploringState.Exploring;
        return true;
    }

    /// <summary>
    /// Marks this subset's set as fully explored.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "setExplored()")]
    internal void SetExplored() => Set.Exploring = OpSet.ExploringState.Explored;

    // ~ Members of the subset --------------------------------------------------

    // The upstream subset reads its `set` field directly, relying on subsets being canonized to the live
    // set before use. Alembic's set-merge leaves dead sets in place, so the subset resolves the live set
    // at the point of use through the planner's equivalence-root primitive.
    OpSet LiveSet => VolcanoPlanner.EquivRoot(Set);

    /// <summary>
    /// The set's representative (original) op, regardless of cost.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getOriginal()")]
    public IOp? GetOriginal() => LiveSet.Rel;

    /// <summary>
    /// The best member if one has been costed, otherwise the original op.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getBestOrOriginal()")]
    public IOp? GetBestOrOriginal() => Best ?? GetOriginal();

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "stripped()")]
    public IOp Stripped => GetBestOrOriginal() ?? this;

    /// <summary>
    /// Every member of this subset: the set's ops whose trait set satisfies this subset's.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getRels()")]
    public IEnumerable<IOp> GetRels()
    {
        foreach (var rel in LiveSet.Ops)
            if (rel.Traits.Satisfies(Traits))
                yield return rel;
    }

    /// <summary>
    /// As <see cref="GetRels"/>, but as a list.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getRelList()")]
    public IList<IOp> GetRelList()
    {
        var list = new List<IOp>();
        foreach (var rel in LiveSet.Ops)
            if (rel.Traits.Satisfies(Traits))
                list.Add(rel);

        return list;
    }

    /// <summary>
    /// Whether <paramref name="op"/> is a member of this subset.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "contains(RelNode)")]
    public bool Contains(IOp op)
    {
        foreach (var rel in LiveSet.Ops)
            if (ReferenceEquals(rel, op))
                return op.Traits.Satisfies(Traits);

        return false;
    }

    /// <summary>
    /// The subsets of this set whose trait set satisfies this subset's.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getSubsetsSatisfyingThis()")]
    public IEnumerable<OpSubset> GetSubsetsSatisfyingThis()
    {
        foreach (var subset in LiveSet.Subsets)
            if (subset.Traits.Satisfies(Traits))
                yield return subset;
    }

    /// <summary>
    /// The subsets of this set whose trait set is satisfied by this subset's.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getSatisfyingSubsets()")]
    public IEnumerable<OpSubset> GetSatisfyingSubsets()
    {
        foreach (var subset in LiveSet.Subsets)
            if (Traits.Satisfies(subset.Traits))
                yield return subset;
    }

    // ~ Parents ----------------------------------------------------------------

    /// <summary>
    /// The ops one of whose inputs is exactly this subset.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getParents()")]
    public IEnumerable<IOp> GetParents()
    {
        var seen = new HashSet<IOp>(ReferenceEqualityComparer.Instance);
        foreach (var parent in LiveSet.Parents)
        {
            foreach (var input in parent.Children)
            {
                if (ReferenceEquals(input, this) && seen.Add(parent))
                {
                    yield return parent;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// The distinct subsets that contain an op one of whose inputs is exactly this subset.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getParentSubsets(VolcanoPlanner)")]
    public IEnumerable<OpSubset> GetParentSubsets(VolcanoPlanner planner)
    {
        var seen = new HashSet<OpSubset>();
        foreach (var parent in LiveSet.Parents)
        {
            foreach (var input in parent.Children)
            {
                var sub = (OpSubset)input;
                if (sub.LiveSet == LiveSet && sub.Traits.Equals(Traits))
                {
                    var parentSubset = planner.GetSubsetNonNull(parent);
                    if (seen.Add(parentSubset))
                        yield return parentSubset;

                    break;
                }
            }
        }
    }

    /// <summary>
    /// The ops one of whose inputs is in this subset (its trait set satisfied by this subset's).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getParentRels()")]
    public IEnumerable<IOp> GetParentRels()
    {
        var live = LiveSet;
        var seen = new HashSet<IOp>(ReferenceEqualityComparer.Instance);
        foreach (var parent in live.Parents)
        {
            foreach (var input in parent.Children)
            {
                var sub = (OpSubset)input;
                if (sub.LiveSet == live && Traits.Satisfies(sub.Traits))
                {
                    if (seen.Add(parent))
                        yield return parent;

                    break;
                }
            }
        }
    }

    /// <summary>
    /// Recursively builds a tree of the cheapest plan at each op, replacing each subset with its best
    /// member.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "buildCheapestPlan(VolcanoPlanner)")]
    internal IOp BuildCheapestPlan(VolcanoPlanner planner)
    {
        var replacer = new CheapestPlanReplacer(planner);
        var cheapest = replacer.Visit(this, -1, null);
        planner.FireOpChosen(null);
        return cheapest;
    }

    /// <summary>
    /// Replaces each subset in a plan with its cheapest member, memoizing by op so a shared subset is
    /// built once.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset.CheapestPlanReplacer")]
    sealed class CheapestPlanReplacer
    {

        readonly VolcanoPlanner _planner;

        readonly Dictionary<IOp, IOp> _visited = new Dictionary<IOp, IOp>(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// Creates a replacer that fires "op chosen" events on <paramref name="planner"/> as it builds.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset.CheapestPlanReplacer", "CheapestPlanReplacer(VolcanoPlanner)")]
        public CheapestPlanReplacer(VolcanoPlanner planner)
        {
            _planner = planner;
        }

        /// <summary>
        /// Returns the cheapest concrete plan rooted at <paramref name="p"/>, replacing each subset with its
        /// best member and recursing into inputs (memoized).
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset.CheapestPlanReplacer", "visit(RelNode, int, RelNode)")]
        public IOp Visit(IOp p, int ordinal, IOp? parent)
        {
            if (_visited.TryGetValue(p, out var prevVisit))
                return prevVisit;

            var key = p;
            if (p is OpSubset subset)
            {
                var cheapest = subset.Best;
                if (cheapest is null)
                    throw new CannotPlanException($"There are not enough rules to produce an op with the requested traits ({subset.Traits.Convention}).");

                p = cheapest;
            }

            if (ordinal != -1)
                _planner.FireOpChosen(p);

            var oldInputs = p.Children;
            var inputs = ImmutableArray.CreateBuilder<IOp>(oldInputs.Length);
            bool changed = false;
            for (int i = 0; i < oldInputs.Length; i++)
            {
                var input = Visit(oldInputs[i], i, p);
                if (!ReferenceEquals(input, oldInputs[i]))
                    changed = true;

                inputs.Add(input);
            }

            var result = changed ? p.Copy(p.Traits, inputs.MoveToImmutable()) : p;
            _visited[key] = result;
            return result;
        }

    }

    /// <summary>
    /// A subset has no cost of its own.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "computeSelfCost(RelOptPlanner, RelMetadataQuery)")]
    public override IOpCost ComputeSelfCost(IOpPlanner planner, OpMetadataQuery mq) => planner.CostFactory.MakeZeroCost();

    /// <summary>
    /// A subset's identity is its set; the trait set is compared by the base. (<see cref="Best"/> is
    /// mutable optimizer state and is deliberately excluded.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "explain(RelWriter)")]
    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("subset", Set.Id);
        return writer;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "copy(RelTraitSet, List<RelNode>)")]
    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        if (children.IsEmpty)
        {
            var traitSet1 = traits.Simplify();
            if (traitSet1.Equals(Traits))
                return this;

            return Set.GetOrCreateSubset(Cluster, traitSet1, IsRequired);
        }

        throw new NotSupportedException();
    }

}
