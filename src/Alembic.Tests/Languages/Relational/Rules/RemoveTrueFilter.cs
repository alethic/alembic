using Alembic.Algebra;

using Alembic.Tests.Languages.Relational.Logical;

using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Simplification: a filter whose predicate is the constant <c>"true"</c> is redundant and is
/// replaced by its input.
/// </summary>
sealed class RemoveTrueFilter : OpRule
{

    public RemoveTrueFilter()
        : base(OperandJ<LogicalFilter>(null, n => ((LogicalFilter)n).Predicate == "true", Any()))
    {
    }

    public override void OnMatch(OpRuleCall call)
    {
        call.TransformTo(((LogicalFilter)call.Op(0)).Input);
    }

}
