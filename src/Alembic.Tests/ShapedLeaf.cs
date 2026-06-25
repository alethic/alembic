using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests;

/// <summary>
/// A toy leaf op carrying a fixed <see cref="Shape"/> as its output type, plus a tag that participates
/// in its digest — so two leaves can match on every term except output type, or match on output type
/// while differing in a term.
/// </summary>
sealed class ShapedLeaf : AbstractOp
{

    readonly Shape _shape;
    readonly int _tag;

    public ShapedLeaf(OpCluster cluster, OpTraitSet traits, Shape shape, int tag = 0)
        : base(cluster, traits)
    {
        _shape = shape;
        _tag = tag;
    }

    protected override IOutputType DeriveOutputType() => _shape;

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("tag", _tag);
        return writer;
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children) => new ShapedLeaf(Cluster, traits, _shape, _tag);

}
