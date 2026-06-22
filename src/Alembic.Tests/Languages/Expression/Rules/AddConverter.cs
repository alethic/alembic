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

    readonly TraitSet _physical;

    public AddConverter(TraitSet physical)
        : base(ExpressionConventions.Logical, ExpressionConventions.Physical)
    {
        _physical = physical;
    }

    public override INode? Convert(INode node)
    {
        if (node is Add add)
            return new PhysicalAdd(_physical, add.Left, add.Right);

        return null;
    }

}
