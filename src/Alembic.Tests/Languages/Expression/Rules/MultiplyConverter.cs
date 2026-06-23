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

    readonly OpTraitSet _physical;

    public MultiplyConverter(OpTraitSet physical)
        : base(ExpressionConventions.Logical, ExpressionConventions.Physical)
    {
        _physical = physical;
    }

    public override bool IsGuaranteed => false;

    public override IOp? Convert(IOp op)
    {
        if (op is Multiply multiply)
            return new PhysicalMultiply(_physical, Convert(multiply.Left, Target), Convert(multiply.Right, Target));

        return null;
    }

}
