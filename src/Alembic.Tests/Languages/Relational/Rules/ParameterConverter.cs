using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Lowers a logical parameter to a physical parameter.
/// </summary>
sealed class ParameterConverter : ConverterRule
{

    readonly TraitSet _physical;

    public ParameterConverter(TraitSet physical)
        : base(RelationalConventions.Logical, RelationalConventions.Physical)
    {
        _physical = physical;
    }

    public override bool IsGuaranteed => true;

    public override IOpNode? Convert(IOpNode op)
    {
        if (op is LogicalParameter parameter)
            return new PhysicalParameter(parameter.Cluster, _physical, parameter.Name);

        return null;
    }

}
