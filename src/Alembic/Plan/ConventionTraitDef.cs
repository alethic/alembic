namespace Alembic.Plan;

/// <summary>
/// The trait dimension for <see cref="IConvention"/>. Always registered first.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef")]
public sealed class ConventionTraitDef : OpTraitDef<IConvention>
{

    /// <summary>
    /// The singleton instance.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef", "INSTANCE")]
    public static readonly ConventionTraitDef Instance = new ConventionTraitDef();

    ConventionTraitDef()
    {

    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef", "getSimpleName()")]
    public override string Name => "convention";

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef", "getDefault()")]
    public override IConvention Default => Convention.None;

}
