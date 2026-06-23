using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// Replaces an <see cref="AbstractConverter"/> with a real conversion built from the registered
/// converter rules. A <see cref="VolcanoPlanner"/> registers one of these automatically.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.AbstractConverter.ExpandConversionRule")]
public sealed class ExpandConversionRule : Rule
{

    public ExpandConversionRule()
        : base(Any<AbstractConverter>())
    {

    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.AbstractConverter.ExpandConversionRule", "onMatch(RelOptRuleCall)")]
    public override void OnMatch(RuleCall call)
    {
        var converter = (AbstractConverter)call.Op(0);
        var planner = (VolcanoPlanner)call.Planner;

        var converted = planner.ChangeTraitsUsingConverters(converter.Input, converter.Traits);
        if (converted is not null)
            call.TransformTo(converted);
    }

}
