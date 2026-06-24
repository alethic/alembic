using Alembic.Algebra;

using Alembic.Tests.Languages.Expression.Logical;

using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Constant folding: a multiplication of two literals collapses to a single literal holding their
/// product.
/// </summary>
sealed class FoldMultiply : OpRule
{

    public FoldMultiply()
        : base(Some<Multiply>(Leaf<Literal>(), Leaf<Literal>()))
    {
    }

    public override void OnMatch(OpRuleCall call)
    {
        var multiply = (Multiply)call.Op(0);
        var left = (Literal)call.Op(1);
        var right = (Literal)call.Op(2);
        call.TransformTo(new Literal(multiply.Cluster, multiply.Traits, left.Value * right.Value));
    }

}
