using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Expression.Logical;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Constant folding: a multiplication of two literals collapses to a single literal holding their
/// product.
/// </summary>
sealed class FoldMultiply : IRule
{

    public Operand Operand => Operand.Of<Multiply>(Operand.Of<Literal>(), Operand.Of<Literal>());

    public void OnMatch(RuleCall call)
    {
        var multiply = (Multiply)call.Node(0);
        var left = (Literal)call.Node(1);
        var right = (Literal)call.Node(2);
        call.Transform(new Literal(multiply.Traits, left.Value * right.Value));
    }

}
