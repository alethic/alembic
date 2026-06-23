using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The members of a <see cref="NodeSet"/> that share one trait set — equivalent plans with identical
/// physical properties. A subset is itself an <see cref="INode"/> so it can stand in as a child of a
/// registered node, and it remembers the cheapest member found so far.
/// </summary>
[Provenance("org.apache.calcite.plan.volcano.RelSubset")]
public sealed class NodeSubset : AbstractNode
{

    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "RelSubset(RelOptCluster, RelSet, RelTraitSet)")]
    internal NodeSubset(NodeSet set, TraitSet traits, ICost infiniteCost)
        : base(set.Cluster, traits, ImmutableArray<INode>.Empty)
    {
        Set = set;
        BestCost = infiniteCost;
        UpperBound = infiniteCost;
    }

    /// <summary>
    /// Where a subset is in its top-down optimization.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset.OptimizeState")]
    public enum OptimizeState
    {

        /// <summary>
        /// Optimization has started but no winner is known yet.
        /// </summary>
        [Provenance("org.apache.calcite.plan.volcano.RelSubset.OptimizeState", "OPTIMIZING")]
        Optimizing,

        /// <summary>
        /// Optimization has finished (with or without a winner).
        /// </summary>
        [Provenance("org.apache.calcite.plan.volcano.RelSubset.OptimizeState", "COMPLETED")]
        Completed

    }

    /// <summary>
    /// The equivalence set this subset belongs to.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getSet()")]
    public NodeSet Set { get; }

    /// <summary>
    /// The cheapest member found so far, or <c>null</c> if none has a finite cost yet.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getBest()")]
    public INode? Best { get; internal set; }

    /// <summary>
    /// The cost of <see cref="Best"/> (infinite until a member is costed).
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "bestCost")]
    public ICost BestCost { get; internal set; }

    /// <summary>
    /// Adds <paramref name="node"/> as an equivalent expression in this subset's set: notifies listeners
    /// that an equivalent has been found, then appends it via <see cref="NodeSet.AddInternal"/>. A node
    /// already in the set is ignored.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "add(RelNode)")]
    internal void Add(INode node)
    {
        if (Set.Nodes.Contains(node))
            return;

        ((AbstractPlanner)Set.Cluster.Planner).FireNodeEquivalenceFound(node, Set.Id, !node.Convention.Equals(Convention.None));
        Set.AddInternal(node);
    }

    // ~ Top-down (Cascades) optimization state ---------------------------------

    /// <summary>
    /// This subset's optimization task state, or <c>null</c> if it has not been optimized.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "taskState")]
    internal OptimizeState? TaskState { get; private set; }

    /// <summary>
    /// The upper bound from the last optimize call (a winner must cost no more than this).
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "upperBound")]
    internal ICost UpperBound { get; set; }

    bool _delivered;
    bool _required;
    bool _enforceDisabled;

    /// <summary>
    /// Whether this subset should trigger rules now that it has become delivered.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "triggerRule")]
    internal bool TriggerRule { get; set; }

    HashSet<INode>? _passThroughCache;

    /// <summary>
    /// Whether some member of this subset delivers its trait set.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "isDelivered()")]
    public bool IsDelivered => _delivered;

    /// <summary>
    /// Whether a parent requires this subset's trait set.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "isRequired()")]
    public bool IsRequired => _required;

    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "setDelivered()")]
    internal void SetDelivered()
    {
        TriggerRule = !_delivered;
        _delivered = true;
    }

    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "setRequired()")]
    internal void SetRequired()
    {
        TriggerRule = false;
        _required = true;
    }

    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "disableEnforcing()")]
    internal void DisableEnforcing() => _enforceDisabled = true;

    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "isEnforceDisabled()")]
    internal bool IsEnforceDisabled => _enforceDisabled;

    /// <summary>
    /// The best cost if this subset is fully optimized and its winner is within the upper bound,
    /// otherwise <c>null</c>.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getWinnerCost()")]
    internal ICost? GetWinnerCost()
    {
        if (TaskState == OptimizeState.Completed && BestCost.IsLessThanOrEqual(UpperBound))
            return BestCost;

        return null;
    }

    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "startOptimize(RelOptCost)")]
    internal void StartOptimize(ICost upperBound)
    {
        if (UpperBound.IsLessThan(upperBound))
        {
            UpperBound = upperBound;
            if (BestCost.IsLessThan(UpperBound))
                UpperBound = BestCost;
        }

        TaskState = OptimizeState.Optimizing;
    }

    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "setOptimized()")]
    internal void SetOptimized() => TaskState = OptimizeState.Completed;

    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "resetTaskState()")]
    internal bool ResetTaskState()
    {
        bool optimized = TaskState is not null;
        TaskState = null;
        UpperBound = BestCost;
        return optimized;
    }

    /// <summary>
    /// Asks <paramref name="node"/> (a physical node) to pass this subset's required traits down to its
    /// inputs, returning the delivering node, or <c>null</c> (also if it has already been asked).
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "passThrough(RelNode)")]
    internal INode? PassThrough(INode node)
    {
        if (node is not IPhysicalNode physical)
            return null;

        _passThroughCache ??= new HashSet<INode>(ReferenceEqualityComparer.Instance);
        if (!_passThroughCache.Add(node))
            return null;

        return physical.PassThrough(Traits);
    }

    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "isExplored()")]
    internal bool IsExplored => Set.Exploring == NodeSet.ExploringState.Explored;

    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "explore()")]
    internal bool Explore()
    {
        if (Set.Exploring is not null)
            return false;

        Set.Exploring = NodeSet.ExploringState.Exploring;
        return true;
    }

    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "setExplored()")]
    internal void SetExplored() => Set.Exploring = NodeSet.ExploringState.Explored;

    // ~ Members of the subset --------------------------------------------------

    // The upstream subset reads its `set` field directly, relying on subsets being canonized to the live
    // set before use. Alembic's set-merge leaves dead sets in place, so the subset resolves the live set
    // at the point of use through the planner's equivalence-root primitive.
    NodeSet LiveSet => VolcanoPlanner.EquivRoot(Set);

    /// <summary>
    /// The set's representative (original) node, regardless of cost.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getOriginal()")]
    public INode? GetOriginal() => LiveSet.Rel;

    /// <summary>
    /// The best member if one has been costed, otherwise the original node.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getBestOrOriginal()")]
    public INode? GetBestOrOriginal() => Best ?? GetOriginal();

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "stripped()")]
    public INode Stripped => GetBestOrOriginal() ?? this;

    /// <summary>
    /// Every member of this subset: the set's nodes whose trait set satisfies this subset's.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getRels()")]
    public IEnumerable<INode> GetRels()
    {
        foreach (var rel in LiveSet.Nodes)
            if (rel.Traits.Satisfies(Traits))
                yield return rel;
    }

    /// <summary>
    /// As <see cref="GetRels"/>, but as a list.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getRelList()")]
    public IList<INode> GetRelList()
    {
        var list = new List<INode>();
        foreach (var rel in LiveSet.Nodes)
            if (rel.Traits.Satisfies(Traits))
                list.Add(rel);

        return list;
    }

    /// <summary>
    /// Whether <paramref name="node"/> is a member of this subset.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "contains(RelNode)")]
    public bool Contains(INode node)
    {
        foreach (var rel in LiveSet.Nodes)
            if (ReferenceEquals(rel, node))
                return node.Traits.Satisfies(Traits);

        return false;
    }

    /// <summary>
    /// The subsets of this set whose trait set satisfies this subset's.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getSubsetsSatisfyingThis()")]
    public IEnumerable<NodeSubset> GetSubsetsSatisfyingThis()
    {
        foreach (var subset in LiveSet.Subsets)
            if (subset.Traits.Satisfies(Traits))
                yield return subset;
    }

    /// <summary>
    /// The subsets of this set whose trait set is satisfied by this subset's.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getSatisfyingSubsets()")]
    public IEnumerable<NodeSubset> GetSatisfyingSubsets()
    {
        foreach (var subset in LiveSet.Subsets)
            if (Traits.Satisfies(subset.Traits))
                yield return subset;
    }

    // ~ Parents ----------------------------------------------------------------

    /// <summary>
    /// The nodes one of whose inputs is exactly this subset.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getParents()")]
    public IEnumerable<INode> GetParents()
    {
        var seen = new HashSet<INode>(ReferenceEqualityComparer.Instance);
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
    /// The distinct subsets that contain a node one of whose inputs is exactly this subset.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getParentSubsets(VolcanoPlanner)")]
    public IEnumerable<NodeSubset> GetParentSubsets(VolcanoPlanner planner)
    {
        var seen = new HashSet<NodeSubset>();
        foreach (var parent in LiveSet.Parents)
        {
            foreach (var input in parent.Children)
            {
                var sub = (NodeSubset)input;
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
    /// The nodes one of whose inputs is in this subset (its trait set satisfied by this subset's).
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "getParentRels()")]
    public IEnumerable<INode> GetParentRels()
    {
        var live = LiveSet;
        var seen = new HashSet<INode>(ReferenceEqualityComparer.Instance);
        foreach (var parent in live.Parents)
        {
            foreach (var input in parent.Children)
            {
                var sub = (NodeSubset)input;
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
    /// Recursively builds a tree of the cheapest plan at each node, replacing each subset with its best
    /// member.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "buildCheapestPlan(VolcanoPlanner)")]
    internal INode BuildCheapestPlan(VolcanoPlanner planner)
    {
        var replacer = new CheapestPlanReplacer(planner);
        var cheapest = replacer.Visit(this, -1, null);
        planner.FireNodeChosen(null);
        return cheapest;
    }

    /// <summary>
    /// Replaces each subset in a plan with its cheapest member, memoizing by node so a shared subset is
    /// built once.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset.CheapestPlanReplacer")]
    sealed class CheapestPlanReplacer
    {

        readonly VolcanoPlanner _planner;

        [Provenance("org.apache.calcite.plan.volcano.RelSubset.CheapestPlanReplacer", "visited")]
        readonly Dictionary<INode, INode> _visited = new Dictionary<INode, INode>(ReferenceEqualityComparer.Instance);

        [Provenance("org.apache.calcite.plan.volcano.RelSubset.CheapestPlanReplacer", "CheapestPlanReplacer(VolcanoPlanner)")]
        public CheapestPlanReplacer(VolcanoPlanner planner)
        {
            _planner = planner;
        }

        [Provenance("org.apache.calcite.plan.volcano.RelSubset.CheapestPlanReplacer", "visit(RelNode, int, RelNode)")]
        public INode Visit(INode p, int ordinal, INode? parent)
        {
            if (_visited.TryGetValue(p, out var prevVisit))
                return prevVisit;

            var key = p;
            if (p is NodeSubset subset)
            {
                var cheapest = subset.Best;
                if (cheapest is null)
                    throw new CannotPlanException($"There are not enough rules to produce a node with the requested traits ({subset.Traits.Convention}).");

                p = cheapest;
            }

            if (ordinal != -1)
                _planner.FireNodeChosen(p);

            var oldInputs = p.Children;
            var inputs = ImmutableArray.CreateBuilder<INode>(oldInputs.Length);
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
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "computeSelfCost(RelOptPlanner, RelMetadataQuery)")]
    public override ICost ComputeSelfCost(IPlanner planner) => planner.CostFactory.MakeZeroCost();

    /// <summary>
    /// A subset's identity is its set; the trait set is compared by the base. (<see cref="Best"/> is
    /// mutable optimizer state and is deliberately excluded.)
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "explain(RelWriter)")]
    public override INodeWriter ExplainTerms(INodeWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("subset", Set.Id);
        return writer;
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.RelSubset", "copy(RelTraitSet, List<RelNode>)")]
    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return this;
    }

}
