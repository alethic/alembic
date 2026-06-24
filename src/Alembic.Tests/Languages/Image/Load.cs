using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Algebra.Metadata;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Loads an image from a named source. The leaf of an image pipeline. Loading reads into host memory,
/// so it is CPU-only — a GPU pipeline must upload after it.
/// </summary>
sealed class Load : AbstractOp, IImageOperation
{

    readonly string _source;

    public Load(OpCluster cluster, OpTraitSet traits, string source)
        : base(cluster, traits)
    {
        _source = source;
    }

    public string Source => _source;

    public bool SupportsGpu => false;

    public override IOpCost ComputeSelfCost(IOpPlanner planner, OpMetadataQuery mq)
    {
        return planner.CostFactory.MakeCost(ImageConventions.OpCost(Traits.Convention), 0);
    }

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("source", _source);
        return writer;
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new Load(Cluster, traits, _source);
    }

}
