using Alembic.Algebra;
using Alembic.Plan;

using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;

using Alembic.Algebra.Convert;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Lowers a logical parameter to a physical parameter.
/// </summary>
sealed class ParameterConverter : ConverterRule
{

    readonly OpTraitSet _physical;

    public ParameterConverter(OpTraitSet physical)
        : base(RelationalConventions.Logical, RelationalConventions.Physical)
    {
        _physical = physical;
    }

    public override bool IsGuaranteed => false;

    public override IOp? Convert(IOp op)
    {
        if (op is LogicalParameter parameter)
            return new PhysicalParameter(parameter.Cluster, _physical, parameter.Name);

        return null;
    }

}
