using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression;

/// <summary>
/// The two conventions of the toy arithmetic-expression language: a logical model the user writes, and
/// a physical model an evaluator could run.
/// </summary>
static class ExpressionConventions
{

    public static readonly Convention Logical = new Convention("EXPR-LOGICAL");

    public static readonly Convention Physical = new ExpressionPhysical();

}
