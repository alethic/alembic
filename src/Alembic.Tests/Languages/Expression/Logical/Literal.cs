using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Logical;

/// <summary>
/// A logical leaf holding a constant integer value.
/// </summary>
sealed class Literal : AbstractOp
{

    readonly int _value;

    public Literal(OpCluster cluster, OpTraitSet traits, int value)
        : base(cluster, traits, ImmutableArray<IOp>.Empty)
    {
        _value = value;
    }

    public int Value => _value;

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("value", _value);
        return writer;
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new Literal(Cluster, traits, _value);
    }

}
