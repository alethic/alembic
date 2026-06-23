namespace Alembic.Plan;

/// <summary>
/// The trait dimension for <see cref="IConvention"/>. Always registered first.
/// </summary>
[Provenance("org.apache.calcite.plan.ConventionTraitDef")]
public sealed class ConventionTraitDef : TraitDef<IConvention>
{

    /// <summary>
    /// The singleton instance.
    /// </summary>
    [Provenance("org.apache.calcite.plan.ConventionTraitDef", "INSTANCE")]
    public static readonly ConventionTraitDef Instance = new ConventionTraitDef();

    ConventionTraitDef()
    {

    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.ConventionTraitDef", "getSimpleName()")]
    public override string Name => "convention";

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.ConventionTraitDef", "getDefault()")]
    public override IConvention Default => Convention.None;

}
