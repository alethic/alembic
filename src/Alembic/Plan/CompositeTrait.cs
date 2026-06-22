using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Alembic.Plan;

/// <summary>
/// The non-generic face of a <see cref="CompositeTrait{T}"/>, so a <see cref="TraitSet"/> can recognize
/// and flatten composite traits without knowing the member type.
/// </summary>
public interface ICompositeTrait : ITrait
{

    /// <summary>
    /// The number of member traits.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// The member trait at the given index.
    /// </summary>
    ITrait TraitAt(int index);

}

/// <summary>
/// A trait that consists of a list of traits, all of the same dimension. It lets a
/// <see cref="TraitSet"/> hold several values on one dimension (e.g. several sort orders).
/// </summary>
/// <typeparam name="T">The member trait type.</typeparam>
public sealed class CompositeTrait<T> : ICompositeTrait
    where T : IMultipleTrait
{

    readonly ITraitDef _def;
    readonly ImmutableArray<T> _traits;

    CompositeTrait(ITraitDef def, ImmutableArray<T> traits)
    {
        _def = def;
        _traits = traits;
    }

    /// <summary>
    /// Creates a trait from the members: the dimension's default when empty, the sole member when there
    /// is one, otherwise a composite.
    /// </summary>
    public static ITrait Of(TraitDef<T> def, IReadOnlyList<T> traits)
    {
        if (traits.Count == 0)
            return def.Default;
        if (traits.Count == 1)
            return traits[0];

        return new CompositeTrait<T>(def, traits.ToImmutableArray());
    }

    /// <summary>
    /// The member traits, in order.
    /// </summary>
    public ImmutableArray<T> Traits => _traits;

    /// <inheritdoc />
    public int Count => _traits.Length;

    /// <inheritdoc />
    public ITrait TraitAt(int index) => _traits[index];

    /// <inheritdoc />
    public ITraitDef TraitDef => _def;

    /// <inheritdoc />
    public bool Satisfies(ITrait other)
    {
        if (other is CompositeTrait<T> composite)
            return composite._traits.All(required => _traits.Any(t => t.Satisfies(required)));

        return _traits.Any(t => t.Satisfies(other));
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is CompositeTrait<T> other && _traits.SequenceEqual(other._traits);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var trait in _traits)
            hash.Add(trait);

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return "[" + string.Join(", ", _traits) + "]";
    }

}
