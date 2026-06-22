using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Loads an image from a named source. The leaf of an image pipeline. Loading reads into host memory,
/// so it is CPU-only — a GPU pipeline must upload after it.
/// </summary>
sealed class Load : AbstractNode, IImageOperation
{

    readonly string _source;

    public Load(Cluster cluster, TraitSet traits, string source)
        : base(cluster, traits, ImmutableArray<INode>.Empty)
    {
        _source = source;
    }

    public string Source => _source;

    public bool SupportsGpu => false;

    public override ICost ComputeSelfCost(IPlanner planner)
    {
        return planner.CostFactory.MakeCost(ImageConventions.OpCost(Traits.Convention), 0);
    }

    public override INodeWriter ExplainTerms(INodeWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("source", _source);
        return writer;
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new Load(Cluster, traits, _source);
    }

}
