using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Expression.Physical;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Lowers a logical multiplication to a physical one, preserving its (separately lowered) operands.
/// </summary>
sealed class MultiplyConverter : ConverterRule
{

    readonly TraitSet _physical;

    public MultiplyConverter(TraitSet physical)
        : base(ExpressionConventions.Logical, ExpressionConventions.Physical)
    {
        _physical = physical;
    }

    public override INode? Convert(INode node)
    {
        if (node is Multiply multiply)
            return new PhysicalMultiply(_physical, multiply.Left, multiply.Right);

        return null;
    }

}
