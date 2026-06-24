using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// Replaces an <see cref="AbstractConverter"/> with a real conversion built from the registered
/// converter rules. A <see cref="VolcanoPlanner"/> registers one of these automatically.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.AbstractConverter.ExpandConversionRule")]
public sealed class ExpandConversionRule : OpRule
{

    /// <summary>
    /// The shared instance a planner registers. (Calcite's <c>INSTANCE = Config.DEFAULT.toRule()</c>;
    /// Alembic has no <c>RelRule.Config</c> framework — see §1 — so the rule takes ctor args directly.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.AbstractConverter.ExpandConversionRule", "INSTANCE")]
    public static readonly ExpandConversionRule Instance = new ExpandConversionRule();

    public ExpandConversionRule()
        : base(Any<AbstractConverter>())
    {

    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.AbstractConverter.ExpandConversionRule", "onMatch(RelOptRuleCall)")]
    public override void OnMatch(OpRuleCall call)
    {
        var converter = (AbstractConverter)call.Op(0);
        var planner = (VolcanoPlanner)call.Planner;

        var converted = planner.ChangeTraitsUsingConverters(converter.Input, converter.Traits);
        if (converted is not null)
            call.TransformTo(converted);
    }

}
