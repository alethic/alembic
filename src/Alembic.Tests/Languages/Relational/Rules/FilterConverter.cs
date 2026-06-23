using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Lowers a logical filter to a physical filter, preserving the (already lowered) input.
/// </summary>
sealed class FilterConverter : ConverterRule
{

    readonly OpTraitSet _physical;

    public FilterConverter(OpTraitSet physical)
        : base(RelationalConventions.Logical, RelationalConventions.Physical)
    {
        _physical = physical;
    }

    public override bool IsGuaranteed => true;

    public override IOp? Convert(IOp op)
    {
        if (op is LogicalFilter filter)
            return new PhysicalFilter(_physical, filter.Input, filter.Predicate);

        return null;
    }

}
