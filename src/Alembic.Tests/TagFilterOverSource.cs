using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational.Logical;

namespace Alembic.Tests;

/// <summary>
/// An operand rule matching a <see cref="LogicalFilter"/> directly over a <see cref="LogicalSource"/>,
/// retagging the filter's predicate. Exercises operand nesting and arity.
/// </summary>
sealed class TagFilterOverSource : Rule
{

    public TagFilterOverSource()
        : base(Some<LogicalFilter>(Leaf<LogicalSource>()))
    {
    }

    public override void OnMatch(RuleCall call)
    {
        var filter = (LogicalFilter)call.Op(0);
        call.TransformTo(new LogicalFilter(filter.Traits, filter.Input, "tagged"));
    }

}
