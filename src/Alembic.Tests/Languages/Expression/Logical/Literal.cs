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

    public Literal(TraitSet traits, int value)
        : base(traits, ImmutableArray<INode>.Empty)
    {
        _value = value;
    }

    public int Value => _value;

    protected override void Explain(INodeWriter writer)
    {
        writer.Item("value", _value);
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new Literal(traits, _value);
    }

}
