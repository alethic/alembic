using Alembic.Algebra;

using Alembic.Tests.Languages.Expression.Physical;

using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Push-down/fusion: a physical addition whose left operand is a physical multiplication collapses
/// into a single <see cref="PhysicalFma"/> that computes <c>a * b + c</c> directly. The right operand
/// is matched by a predicate operand with no children, so it may be any sub-expression.
/// </summary>
sealed class FuseMultiplyAdd : OpRule
{

    public FuseMultiplyAdd()
        : base(Operand<PhysicalAdd>(Some(Operand<PhysicalMultiply>(Any()), Operand<IOp>(Any()))))
    {
    }

    public override void OnMatch(OpRuleCall call)
    {
        var add = (PhysicalAdd)call.Op(0);
        var product = (PhysicalMultiply)call.Op(1);
        call.TransformTo(new PhysicalFma(add.Traits, product.Left, product.Right, add.Right));
    }

}
