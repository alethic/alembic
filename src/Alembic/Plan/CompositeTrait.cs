using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Alembic.Plan;

/// <summary>
/// The non-generic base of a <see cref="CompositeTrait{T}"/>, so a <see cref="TraitSet"/> can recognize
/// and flatten composite traits without knowing the member type.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait")]
public abstract class CompositeTrait : ITrait
{

    /// <summary>
    /// The number of member traits.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "size()")]
    public abstract int Count { get; }

    /// <summary>
    /// The member trait at the given index.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "trait(int)")]
    public abstract ITrait TraitAt(int index);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "getTraitDef()")]
    public abstract TraitDef TraitDef { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "satisfies(RelTrait)")]
    public abstract bool Satisfies(ITrait other);

}

/// <summary>
/// A trait that consists of a list of traits, all of the same dimension. It lets a
/// <see cref="TraitSet"/> hold several values on one dimension (e.g. several sort orders).
/// </summary>
/// <typeparam name="T">The member trait type.</typeparam>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait")]
public sealed class CompositeTrait<T> : CompositeTrait
    where T : class, IMultipleTrait
{

    readonly TraitDef _def;
    readonly ImmutableArray<T> _traits;

    CompositeTrait(TraitDef def, ImmutableArray<T> traits)
    {
        _def = def;
        _traits = traits;
    }

    /// <summary>
    /// Creates a trait from the members: the dimension's default when empty, the sole member when there
    /// is one, otherwise a composite.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "of(RelTraitDef, List)")]
    public static ITrait Of(TraitDef<T> def, IReadOnlyList<T> traits)
    {
        if (traits.Count == 0)
            return def.Default;
        if (traits.Count == 1)
            return def.Canonize(traits[0]);

        // Canonize each member, then the whole composite, so equal composites share one instance.
        var canonized = ImmutableArray.CreateBuilder<T>(traits.Count);
        foreach (var trait in traits)
            canonized.Add((T)def.Canonize(trait));

        return def.Canonize(new CompositeTrait<T>(def, canonized.MoveToImmutable()));
    }

    /// <summary>
    /// The member traits, in order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "traitList()")]
    public ImmutableArray<T> Traits => _traits;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "size()")]
    public override int Count => _traits.Length;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "trait(int)")]
    public override ITrait TraitAt(int index) => _traits[index];

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "getTraitDef()")]
    public override TraitDef TraitDef => _def;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "satisfies(RelTrait)")]
    public override bool Satisfies(ITrait other)
    {
        if (other is CompositeTrait<T> composite)
            return composite._traits.All(required => _traits.Any(t => t.Satisfies(required)));

        return _traits.Any(t => t.Satisfies(other));
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "equals(Object)")]
    public override bool Equals(object? obj)
    {
        return obj is CompositeTrait<T> other && _traits.SequenceEqual(other._traits);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "hashCode()")]
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var trait in _traits)
            hash.Add(trait);

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "toString()")]
    public override string ToString()
    {
        return "[" + string.Join(", ", _traits) + "]";
    }

}
