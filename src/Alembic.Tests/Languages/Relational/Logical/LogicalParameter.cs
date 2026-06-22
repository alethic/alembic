using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Logical;

/// <summary>
/// A logical leaf standing for a bound parameter or relation variable (an input supplied by name
/// rather than a named table).
/// </summary>
sealed class LogicalParameter : AbstractNode
{

    readonly string _name;

    public LogicalParameter(TraitSet traits, string name)
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
        return new LogicalParameter(traits, _name);
    }

}
