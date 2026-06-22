using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational.Logical;

namespace Alembic.Tests;

/// <summary>
/// An operand rule matching a <see cref="LogicalFilter"/> directly over a <see cref="LogicalSource"/>,
/// retagging the filter's predicate. Exercises operand nesting and arity.
/// </summary>
sealed class TagFilterOverSource : IRule
{

    public Operand Operand => Operand.Of<LogicalFilter>(Operand.Of<LogicalSource>());

    public void OnMatch(RuleCall call)
    {
        var filter = (LogicalFilter)call.Node(0);
        call.Transform(new LogicalFilter(filter.Traits, filter.Input, "tagged"));
    }

}
