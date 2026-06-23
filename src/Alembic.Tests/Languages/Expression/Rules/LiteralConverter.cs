using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Expression.Physical;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Lowers a logical literal to a physical literal.
/// </summary>
sealed class LiteralConverter : ConverterRule
{

    readonly OpTraitSet _physical;

    public LiteralConverter(OpTraitSet physical)
        : base(ExpressionConventions.Logical, ExpressionConventions.Physical)
    {
        _physical = physical;
    }

    public override bool IsGuaranteed => true;

    public override IOp? Convert(IOp op)
    {
        if (op is Literal literal)
            return new PhysicalLiteral(literal.Cluster, _physical, literal.Value);

        return null;
    }

}
