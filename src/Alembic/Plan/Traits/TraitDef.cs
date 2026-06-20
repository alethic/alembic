namespace Alembic.Plan.Traits;

/// <summary>
/// Strongly-typed base for a trait dimension. A def of <typeparamref name="TTrait"/> yields
/// <typeparamref name="TTrait"/> values from a <see cref="TraitSet"/>.
/// </summary>
/// <typeparam name="TTrait">The trait type carried on this dimension.</typeparam>
public abstract class TraitDef<TTrait> : ITraitDef
    where TTrait : ITrait
{

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>
    /// The value a node carries on this dimension when none is specified.
    /// </summary>
    public abstract TTrait Default { get; }

    ITrait ITraitDef.Default => Default;

}
