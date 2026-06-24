using Alembic.Algebra;

using Alembic.Tests.Languages.Relational.Logical;

using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Simplification: a filter directly over another filter collapses into one filter whose predicate
/// is the conjunction of the two.
/// </summary>
sealed class MergeFilters : OpRule
{

    public MergeFilters()
        : base(Operand<LogicalFilter>(Some(Operand<LogicalFilter>(Any()))))
    {
    }

    public override void OnMatch(OpRuleCall call)
    {
        var outer = (LogicalFilter)call.Op(0);
        var inner = (LogicalFilter)call.Op(1);
        call.TransformTo(new LogicalFilter(outer.Traits, inner.Input, $"{outer.Predicate} AND {inner.Predicate}"));
    }

}
