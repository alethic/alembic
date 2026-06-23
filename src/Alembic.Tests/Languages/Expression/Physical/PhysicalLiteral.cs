using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical leaf holding a constant integer value.
/// </summary>
sealed class PhysicalLiteral : AbstractOp
{

    readonly int _value;

    public PhysicalLiteral(OpCluster cluster, OpTraitSet traits, int value)
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
        return new PhysicalLiteral(Cluster, traits, _value);
    }

}
