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

    readonly OpTraitSet _physical;

    public SourceConverter(OpTraitSet physical)
        : base(RelationalConventions.Logical, RelationalConventions.Physical)
    {
        _physical = physical;
    }

    public override bool IsGuaranteed => true;

    public override IOp? Convert(IOp op)
    {
        if (op is LogicalSource source)
            return new PhysicalSource(source.Cluster, _physical, source.Table);

        return null;
    }

}
