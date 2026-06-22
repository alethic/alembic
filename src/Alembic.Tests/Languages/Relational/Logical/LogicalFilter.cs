using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Logical;

/// <summary>
/// A logical filter over a single relational input.
/// </summary>
sealed class LogicalFilter : SingleNode
{

    readonly string _predicate;

    public LogicalFilter(TraitSet traits, INode input, string predicate)
        : base(traits, input)
    {
        _predicate = predicate;
    }

    public INode Input => Child;

    public string Predicate => _predicate;

    protected override void Explain(INodeWriter writer)
    {
        base.Explain(writer);
        writer.Item("predicate", _predicate);
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new LogicalFilter(traits, children[0], _predicate);
    }

}
