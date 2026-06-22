using System.Collections.Generic;

using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// An equivalence set: a group of nodes that all produce the same result, partitioned into
/// <see cref="NodeSubset"/>s by trait set. The planner keeps the cheapest member of each subset.
/// </summary>
public sealed class NodeSet
{

    readonly ICostFactory _costFactory;

    internal NodeSet(int id, ICostFactory costFactory)
    {
        Id = id;
        _costFactory = costFactory;
    }

    /// <summary>
    /// This set's stable identity.
    /// </summary>
    public int Id { get; }

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

    internal NodeSubset Add(INode node)
    {
        var subset = GetOrCreateSubset(node.Traits);
        if (!Nodes.Contains(node))
            Nodes.Add(node);

        return subset;
    }

}
