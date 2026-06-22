using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical leaf naming a variable whose value is supplied at evaluation time.
/// </summary>
sealed class PhysicalVariable : AbstractNode
{

    readonly string _name;

    public PhysicalVariable(TraitSet traits, string name)
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
        return new PhysicalVariable(traits, _name);
    }

}
