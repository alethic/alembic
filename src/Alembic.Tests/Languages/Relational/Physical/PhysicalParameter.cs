using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// The physical counterpart of a logical parameter.
/// </summary>
sealed class PhysicalParameter : AbstractNode
{

    readonly string _name;

    public PhysicalParameter(TraitSet traits, string name)
        : base(traits, ImmutableArray<INode>.Empty)
    {
        _name = name;
    }

    public string Name => _name;

    protected override void Explain(INodeWriter writer)
    {
        writer.Item("name", _name);
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new PhysicalParameter(traits, _name);
    }

}
