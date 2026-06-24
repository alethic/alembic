using Alembic.Algebra;
using Alembic.Plan;

using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Expression.Physical;

using Alembic.Algebra.Convert;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Lowers a logical variable to a physical variable.
/// </summary>
sealed class VariableConverter : ConverterRule
{

    readonly OpTraitSet _physical;

    public VariableConverter(OpTraitSet physical)
        : base(ExpressionConventions.Logical, ExpressionConventions.Physical)
    {
        _physical = physical;
    }

    public override bool IsGuaranteed => false;

    public override IOp? Convert(IOp op)
    {
        if (op is Variable variable)
            return new PhysicalVariable(variable.Cluster, _physical, variable.Name);

        return null;
    }

}
