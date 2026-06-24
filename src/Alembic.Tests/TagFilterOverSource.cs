using Alembic.Algebra;

using Alembic.Tests.Languages.Relational.Logical;

using Alembic.Plan;

namespace Alembic.Tests;

/// <summary>
/// An operand rule matching a <see cref="LogicalFilter"/> directly over a <see cref="LogicalSource"/>,
/// retagging the filter's predicate. Exercises operand nesting and arity.
/// </summary>
sealed class TagFilterOverSource : OpRule
{

    public TagFilterOverSource()
        : base(Operand<LogicalFilter>(Some(Operand<LogicalSource>(None()))))
    {
    }

    public override void OnMatch(OpRuleCall call)
    {
        var filter = (LogicalFilter)call.Op(0);
        call.TransformTo(new LogicalFilter(filter.Traits, filter.Input, "tagged"));
    }

}
