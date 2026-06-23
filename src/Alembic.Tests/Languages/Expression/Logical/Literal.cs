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

    public Literal(Cluster cluster, TraitSet traits, int value)
        : base(cluster, traits, ImmutableArray<IOpNode>.Empty)
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

    public override IOpNode Copy(TraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new Literal(Cluster, traits, _value);
    }

}
