using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational.Logical;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Simplification: a filter whose predicate is the constant <c>"true"</c> is redundant and is
/// replaced by its input.
/// </summary>
sealed class RemoveTrueFilter : Rule
{

    public RemoveTrueFilter()
        : base(Any<LogicalFilter>(n => ((LogicalFilter)n).Predicate == "true"))
    {
    }

    public override void OnMatch(RuleCall call)
    {
        call.TransformTo(((LogicalFilter)call.Op(0)).Input);
    }

}
