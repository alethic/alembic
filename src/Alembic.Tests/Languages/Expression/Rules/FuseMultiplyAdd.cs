using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Expression.Physical;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Push-down/fusion: a physical addition whose left operand is a physical multiplication collapses
/// into a single <see cref="PhysicalFma"/> that computes <c>a * b + c</c> directly. The right operand
/// is matched by a predicate operand with no children, so it may be any sub-expression.
/// </summary>
sealed class FuseMultiplyAdd : Rule
{

    public FuseMultiplyAdd()
        : base(Some<PhysicalAdd>(Any<PhysicalMultiply>(), Any<INode>()))
    {
    }

    public override void OnMatch(RuleCall call)
    {
        var add = (PhysicalAdd)call.Node(0);
        var product = (PhysicalMultiply)call.Node(1);
        call.TransformTo(new PhysicalFma(add.Traits, product.Left, product.Right, add.Right));
    }

}
