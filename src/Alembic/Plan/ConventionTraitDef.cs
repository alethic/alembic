namespace Alembic.Plan;

/// <summary>
/// The trait dimension for <see cref="IConvention"/>. Always registered first.
/// </summary>
public sealed class ConventionTraitDef : TraitDef<IConvention>
{

    /// <summary>
    /// The singleton instance.
    /// </summary>
    public static readonly ConventionTraitDef Instance = new ConventionTraitDef();

    ConventionTraitDef()
    {

    }

    /// <inheritdoc />
    public override string Name => "convention";

    /// <inheritdoc />
    public override IConvention Default => Convention.None;

}
