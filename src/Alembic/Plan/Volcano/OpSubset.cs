using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The members of a <see cref="OpSet"/> that share one trait set — equivalent plans with identical
/// physical properties. A subset is itself an <see cref="IOpNode"/> so it can stand in as a child of a
/// registered op, and it remembers the cheapest member found so far.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset")]
public sealed class OpSubset : AbstractOp
{

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "RelSubset(RelOptCluster, RelSet, RelTraitSet)")]
    internal OpSubset(OpSet set, OpTraitSet traits, IOpCost infiniteCost)
        : base(set.Cluster, traits, ImmutableArray<IOpNode>.Empty)
    {
        Set = set;
        BestCost = infiniteCost;
        UpperBound = infiniteCost;
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
    public OpSet Set { get; }

    /// <summary>
    /// The cheapest member found so far, or <c>null</c> if none has a finite cost yet.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getBest()")]
    public IOpNode? Best { get; internal set; }

    /// <summary>
    /// The cost of <see cref="Best"/> (infinite until a member is costed).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "bestCost")]
    public IOpCost BestCost { get; internal set; }

    /// <summary>
    /// Adds <paramref name="op"/> as an equivalent expression in this subset's set: notifies listeners
    /// that an equivalent has been found, then appends it via <see cref="OpSet.AddInternal"/>. An op
    /// already in the set is ignored.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "add(RelNode)")]
    internal void Add(IOpNode op)
    {
        if (Set.Ops.Contains(op))
            return;

        ((AbstractOpPlanner)Set.Cluster.Planner).FireOpEquivalenceFound(op, Set.Id, !op.Convention.Equals(Convention.None));
        Set.AddInternal(op);
    }

    // ~ Top-down (Cascades) optimization state ---------------------------------

    /// <summary>
    /// This subset's optimization task state, or <c>null</c> if it has not been optimized.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "taskState")]
    internal OptimizeState? TaskState { get; private set; }

    /// <summary>
    /// The upper bound from the last optimize call (a winner must cost no more than this).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "upperBound")]
    internal IOpCost UpperBound { get; set; }

    bool _delivered;
    bool _required;
    bool _enforceDisabled;

    /// <summary>
    /// Whether this subset should trigger rules now that it has become delivered.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "triggerRule")]
    internal bool TriggerRule { get; set; }

    HashSet<IOpNode>? _passThroughCache;

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

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "setDelivered()")]
    internal void SetDelivered()
    {
        TriggerRule = !_delivered;
        _delivered = true;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "setRequired()")]
    internal void SetRequired()
    {
        TriggerRule = false;
        _required = true;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "disableEnforcing()")]
    internal void DisableEnforcing() => _enforceDisabled = true;

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

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "setOptimized()")]
    internal void SetOptimized() => TaskState = OptimizeState.Completed;

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
    internal IOpNode? PassThrough(IOpNode op)
    {
        if (op is not IPhysicalNode physical)
            return null;

        _passThroughCache ??= new HashSet<IOpNode>(ReferenceEqualityComparer.Instance);
        if (!_passThroughCache.Add(op))
            return null;

        return physical.PassThrough(Traits);
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "isExplored()")]
    internal bool IsExplored => Set.Exploring == OpSet.ExploringState.Explored;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "explore()")]
    internal bool Explore()
    {
        if (Set.Exploring is not null)
            return false;

        Set.Exploring = OpSet.ExploringState.Exploring;
        return true;
    }

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
    public IOpNode? GetOriginal() => LiveSet.Rel;

    /// <summary>
    /// The best member if one has been costed, otherwise the original op.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getBestOrOriginal()")]
    public IOpNode? GetBestOrOriginal() => Best ?? GetOriginal();

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "stripped()")]
    public IOpNode Stripped => GetBestOrOriginal() ?? this;

    /// <summary>
    /// Every member of this subset: the set's ops whose trait set satisfies this subset's.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getRels()")]
    public IEnumerable<IOpNode> GetRels()
    {
        foreach (var rel in LiveSet.Ops)
            if (rel.Traits.Satisfies(Traits))
                yield return rel;
    }

    /// <summary>
    /// As <see cref="GetRels"/>, but as a list.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "getRelList()")]
    public IList<IOpNode> GetRelList()
    {
        var list = new List<IOpNode>();
        foreach (var rel in LiveSet.Ops)
            if (rel.Traits.Satisfies(Traits))
                list.Add(rel);

        return list;
    }

    /// <summary>
    /// Whether <paramref name="op"/> is a member of this subset.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset", "contains(RelNode)")]
    public bool Contains(IOpNode op)
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
    public IEnumerable<IOpNode> GetParents()
    {
        var seen = new HashSet<IOpNode>(ReferenceEqualityComparer.Instance);
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
    public IEnumerable<IOpNode> GetParentRels()
    {
        var live = LiveSet;
        var seen = new HashSet<IOpNode>(ReferenceEqualityComparer.Instance);
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
    internal IOpNode BuildCheapestPlan(VolcanoPlanner planner)
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

        readonly Dictionary<IOpNode, IOpNode> _visited = new Dictionary<IOpNode, IOpNode>(ReferenceEqualityComparer.Instance);

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset.CheapestPlanReplacer", "CheapestPlanReplacer(VolcanoPlanner)")]
        public CheapestPlanReplacer(VolcanoPlanner planner)
        {
            _planner = planner;
        }

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSubset.CheapestPlanReplacer", "visit(RelNode, int, RelNode)")]
        public IOpNode Visit(IOpNode p, int ordinal, IOpNode? parent)
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
            var inputs = ImmutableArray.CreateBuilder<IOpNode>(oldInputs.Length);
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
    public override IOpCost ComputeSelfCost(IOpPlanner planner) => planner.CostFactory.MakeZeroCost();

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
    public override IOpNode Copy(OpTraitSet traits, ImmutableArray<IOpNode> children)
    {
        return this;
    }

}
