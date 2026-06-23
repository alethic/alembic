using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Expression.Physical;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Lowers a logical variable to a physical variable.
/// </summary>
sealed class VariableConverter : ConverterRule
{

    readonly TraitSet _physical;

    public VariableConverter(TraitSet physical)
        : base(ExpressionConventions.Logical, ExpressionConventions.Physical)
    {
        _physical = physical;
    }

    public override bool IsGuaranteed => true;

    public override IOpNode? Convert(IOpNode op)
    {
        if (op is Variable variable)
            return new PhysicalVariable(variable.Cluster, _physical, variable.Name);

        return null;
    }

}
