using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Expression.Logical;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Constant folding: an addition of two literals collapses to a single literal holding their sum.
/// Exercises a binary operand whose two children are themselves matched.
/// </summary>
sealed class FoldAdd : IRule
{

    public Operand Operand => Operand.Of<Add>(Operand.Of<Literal>(), Operand.Of<Literal>());

    public void OnMatch(RuleCall call)
    {
        var add = (Add)call.Node(0);
        var left = (Literal)call.Node(1);
        var right = (Literal)call.Node(2);
        call.Transform(new Literal(add.Traits, left.Value + right.Value));
    }

}
