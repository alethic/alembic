using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Expression.Physical;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Lowers a logical addition to a physical one, preserving its (separately lowered) operands.
/// </summary>
sealed class AddConverter : ConverterRule
{

    readonly OpTraitSet _physical;

    public AddConverter(OpTraitSet physical)
        : base(ExpressionConventions.Logical, ExpressionConventions.Physical)
    {
        _physical = physical;
    }

    public override bool IsGuaranteed => false;

    public override IOp? Convert(IOp op)
    {
        if (op is Add add)
            return new PhysicalAdd(_physical, Convert(add.Left, Target), Convert(add.Right, Target));

        return null;
    }

}
