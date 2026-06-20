namespace Alembic.Plan.Traits;

/// <summary>
/// The trait dimension for <see cref="Convention"/>. Always registered first.
/// </summary>
public sealed class ConventionTraitDef : TraitDef<Convention>
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
    public override Convention Default => Convention.None;

}
