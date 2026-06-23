using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Expression.Logical;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Constant folding: an addition of two literals collapses to a single literal holding their sum.
/// Exercises a binary operand whose two children are themselves matched.
/// </summary>
sealed class FoldAdd : OpRule
{

    public FoldAdd()
        : base(Some<Add>(Leaf<Literal>(), Leaf<Literal>()))
    {
    }

    public override void OnMatch(OpRuleCall call)
    {
        var add = (Add)call.Op(0);
        var left = (Literal)call.Op(1);
        var right = (Literal)call.Op(2);
        call.TransformTo(new Literal(add.Cluster, add.Traits, left.Value + right.Value));
    }

}
