using System.Collections.Generic;

using Alembic.Algebra;
using Alembic.Util;

namespace Alembic.Plan.Volcano;

/// <summary>
/// An equivalence set: a group of nodes that all produce the same result, partitioned into
/// <see cref="NodeSubset"/>s by trait set. The planner keeps the cheapest member of each subset.
/// </summary>
public sealed class NodeSet
{

    readonly ICostFactory _costFactory;

    // Trait-set pairs already wired with a converter, so each conversion is seeded at most once.
    readonly HashSet<Pair<TraitSet, TraitSet>> _conversions = new HashSet<Pair<TraitSet, TraitSet>>();

    internal NodeSet(int id, ICostFactory costFactory, Cluster cluster)
    {
        Id = id;
        _costFactory = costFactory;
        Cluster = cluster;
    }

    /// <summary>
    /// This set's stable identity.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// The cluster the set's nodes belong to.
    /// </summary>
    public Cluster Cluster { get; }

    /// <summary>
    /// Every node in the set (across all subsets).
    /// </summary>
    public List<INode> Nodes { get; } = new List<INode>();

    /// <summary>
    /// The subsets of this set, one per distinct trait set.
    /// </summary>
    public List<NodeSubset> Subsets { get; } = new List<NodeSubset>();

    /// <summary>
    /// Nodes (in other sets) that reference a subset of this set as a child.
    /// </summary>
    public List<INode> Parents { get; } = new List<INode>();

    /// <summary>
    /// Set when this set is merged into another; the live set is reached by following the chain.
    /// </summary>
    public NodeSet? EquivalentSet { get; internal set; }

    /// <summary>
    /// How far the top-down search has explored this set (applied transformation rules to its members).
    /// </summary>
    public enum ExploringState
    {

        /// <summary>
        /// Exploration is underway.
        /// </summary>
        Exploring,

        /// <summary>
        /// The set is fully explored.
        /// </summary>
        Explored

    }

    /// <summary>
    /// This set's exploration state, or <c>null</c> if exploration has not started.
    /// </summary>
    internal ExploringState? Exploring { get; set; }

    /// <summary>
    /// The first node registered in this set — its representative expression, used as a fallback when a
    /// subset has no best member yet.
    /// </summary>
    public INode? Rel { get; private set; }

    /// <summary>
    /// The subset with exactly the given traits, or <c>null</c>.
    /// </summary>
    public NodeSubset? GetSubset(TraitSet traits)
    {
        foreach (var subset in Subsets)
            if (subset.Traits.Equals(traits))
                return subset;

        return null;
    }

    internal NodeSubset GetOrCreateSubset(TraitSet traits)
    {
        var subset = GetSubset(traits);
        if (subset is null)
        {
            subset = new NodeSubset(this, traits, _costFactory.MakeInfiniteCost());
            Subsets.Add(subset);
        }

        return subset;
    }

    /// <summary>
    /// Gets or creates the subset for the given traits, marking it required (a parent asks for it) or
    /// delivered (a member produces it). A logical (<see cref="Convention.None"/>) subset is neither.
    /// When a subset is freshly created — or first becomes required/delivered — converters/enforcers are
    /// seeded between it and the complementary subsets so the planner can convert between them.
    /// </summary>
    internal NodeSubset GetOrCreateSubset(TraitSet traits, bool required)
    {
        var needsConverter = false;
        var subset = GetSubset(traits);
        if (subset is null)
        {
            needsConverter = true;
            subset = new NodeSubset(this, traits, _costFactory.MakeInfiniteCost());
            Subsets.Add(subset);
        }
        else if ((required && !subset.IsRequired) || (!required && !subset.IsDelivered))
        {
            needsConverter = true;
        }

        if (subset.Traits.Convention.Equals(Convention.None))
            needsConverter = false;
        else if (required)
            subset.SetRequired();
        else
            subset.SetDelivered();

        if (needsConverter)
            AddConverters(subset, required, useAbstractConverter: !((VolcanoPlanner)Cluster.Planner).TopDownOpt);

        return subset;
    }

    /// <summary>
    /// Seeds converters (or convention enforcers) between <paramref name="subset"/> and the complementary
    /// subsets of this set: when <paramref name="subset"/> is required, from each delivered subset to it;
    /// when delivered, from it to each required subset. A conversion is seeded only once per trait-set
    /// pair, and only when the dimensions actually differ and the trait dimension can convert.
    /// </summary>
    void AddConverters(NodeSubset subset, bool required, bool useAbstractConverter)
    {
        var planner = (VolcanoPlanner)Cluster.Planner;

        var others = new List<NodeSubset>();
        foreach (var n in Subsets)
            if (required ? n.IsDelivered : n.IsRequired)
                others.Add(n);

        foreach (var other in others)
        {
            var from = subset;
            var to = other;
            if (required)
            {
                from = other;
                to = subset;
            }

            if (from == to
                || to.IsEnforceDisabled
                || (useAbstractConverter
                    && !from.Traits.Convention.UseAbstractConvertersForConversion(from.Traits, to.Traits)))
            {
                continue;
            }

            if (!_conversions.Add(Pair.Of(from.Traits, to.Traits)))
                continue;

            var needsConverter = false;
            foreach (var fromTrait in to.Traits.Difference(from.Traits))
            {
                var traitDef = fromTrait.TraitDef;
                var toTrait = to.Traits.Get(traitDef);

                if (!traitDef.CanConvert(planner, fromTrait, toTrait))
                {
                    needsConverter = false;
                    break;
                }

                if (!fromTrait.Satisfies(toTrait))
                    needsConverter = true;
            }

            if (needsConverter)
            {
                var enforcer = useAbstractConverter
                    ? new AbstractConverter(to.Traits, from)
                    : subset.Traits.Convention.Enforce(from, to.Traits);

                if (enforcer is not null)
                    planner.Register(enforcer, to);
            }
        }
    }

    /// <summary>
    /// Adds <paramref name="node"/> to the set, placing it in the subset for its traits (creating that
    /// subset if needed) and returning the subset.
    /// </summary>
    internal NodeSubset Add(INode node)
    {
        var subset = GetOrCreateSubset(node.Traits, node.IsEnforcer);
        subset.Add(node);
        return subset;
    }

    /// <summary>
    /// Appends <paramref name="node"/> to the set's node list (if not already present) and records it as
    /// the set's representative when the set has none yet. The equivalence-found notification is fired by
    /// the caller, <see cref="NodeSubset.Add"/>.
    /// </summary>
    internal void AddInternal(INode node)
    {
        if (!Nodes.Contains(node))
            Nodes.Add(node);

        Rel ??= node;
    }

    /// <summary>
    /// Absorbs <paramref name="other"/> into this set: <paramref name="other"/> becomes equivalent to
    /// this set, its nodes and parents move here, and the parents are re-registered to point at the
    /// surviving set. The caller (<see cref="VolcanoPlanner.Merge"/>) has already resolved both sets to
    /// their equivalence roots.
    /// </summary>
    internal void MergeWith(VolcanoPlanner planner, NodeSet other)
    {
        other.EquivalentSet = this;
        planner.RemoveSet(other);

        foreach (var node in other.Nodes)
        {
            var subset = GetOrCreateSubset(node.Traits, node.IsEnforcer);
            AddInternal(node);
            planner.MapNodeToSubset(node, subset);
            planner.PropagateCostImprovements(node);
        }

        // Parents referenced subsets of the now-dead set as children; re-point them at the surviving
        // set, recomputing their digests. Snapshot first, since re-pointing mutates the parent lists.
        var parents = new List<INode>(other.Parents);
        Parents.AddRange(other.Parents);
        other.Parents.Clear();

        foreach (var node in other.Nodes)
            planner.FireRules(node);

        foreach (var parent in parents)
            planner.Rename(parent);

        planner.RuleDriver.OnSetMerged(this);
    }

}
