using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Lowers a logical source to a physical source.
/// </summary>
sealed class SourceConverter : ConverterRule
{

    readonly TraitSet _physical;

    public SourceConverter(TraitSet physical)
        : base(RelationalConventions.Logical, RelationalConventions.Physical)
    {
        _physical = physical;
    }

    public override INode? Convert(INode node)
    {
        if (node is LogicalSource source)
            return new PhysicalSource(source.Cluster, _physical, source.Table);

        return null;
    }

}
