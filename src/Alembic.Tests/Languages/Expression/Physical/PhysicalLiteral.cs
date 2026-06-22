using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical leaf holding a constant integer value.
/// </summary>
sealed class PhysicalLiteral : AbstractNode
{

    readonly int _value;

    public PhysicalLiteral(Cluster cluster, TraitSet traits, int value)
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
        return new PhysicalLiteral(Cluster, traits, _value);
    }

}
