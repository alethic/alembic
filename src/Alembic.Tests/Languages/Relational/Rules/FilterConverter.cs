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

    readonly TraitSet _physical;

    public FilterConverter(TraitSet physical)
        : base(RelationalConventions.Logical, RelationalConventions.Physical)
    {
        _physical = physical;
    }

    public override INode? Convert(INode node)
    {
        if (node is LogicalFilter filter)
            return new PhysicalFilter(_physical, filter.Input, filter.Predicate);

        return null;
    }

}
