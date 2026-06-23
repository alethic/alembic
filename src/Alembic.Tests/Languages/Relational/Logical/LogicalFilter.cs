using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Logical;

/// <summary>
/// A logical filter over a single relational input.
/// </summary>
sealed class LogicalFilter : SingleOp
{

    readonly string _predicate;

    public LogicalFilter(TraitSet traits, IOpNode input, string predicate)
        : base(traits, input)
    {
        _predicate = predicate;
    }

    public IOpNode Input => Child;

    public string Predicate => _predicate;

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("predicate", _predicate);
        return writer;
    }

    public override IOpNode Copy(TraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new LogicalFilter(traits, children[0], _predicate);
    }

}
