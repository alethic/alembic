using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational.Logical;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Simplification: a filter directly over another filter collapses into one filter whose predicate
/// is the conjunction of the two.
/// </summary>
sealed class MergeFilters : IRule
{

    public Operand Operand => Operand.Of<LogicalFilter>(Operand.Of<LogicalFilter>());

    public void OnMatch(RuleCall call)
    {
        var outer = (LogicalFilter)call.Node(0);
        var inner = (LogicalFilter)call.Node(1);
        call.Transform(new LogicalFilter(outer.Traits, inner.Input, $"{outer.Predicate} AND {inner.Predicate}"));
    }

}
