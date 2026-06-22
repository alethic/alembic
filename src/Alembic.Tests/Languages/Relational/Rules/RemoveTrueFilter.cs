using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational.Logical;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Simplification: a filter whose predicate is the constant <c>"true"</c> is redundant and is
/// replaced by its input.
/// </summary>
sealed class RemoveTrueFilter : IRule
{

    public Operand Operand => Operand.Of<LogicalFilter>(filter => filter.Predicate == "true");

    public void OnMatch(RuleCall call)
    {
        call.Transform(((LogicalFilter)call.Node(0)).Input);
    }

}
