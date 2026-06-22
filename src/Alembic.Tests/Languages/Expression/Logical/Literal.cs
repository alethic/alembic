using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Logical;

/// <summary>
/// A logical leaf holding a constant integer value.
/// </summary>
sealed class Literal : AbstractNode
{

    readonly int _value;

    public Literal(Cluster cluster, TraitSet traits, int value)
        : base(cluster, traits, ImmutableArray<INode>.Empty)
    {
        _value = value;
    }

    public int Value => _value;

    public override INodeWriter ExplainTerms(INodeWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("value", _value);
        return writer;
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new Literal(Cluster, traits, _value);
    }

}
