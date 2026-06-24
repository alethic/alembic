using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Alembic.Plan;

/// <summary>
/// The non-generic base of a <see cref="OpCompositeTrait{T}"/>, so a <see cref="OpTraitSet"/> can recognize
/// and flatten composite traits without knowing the member type.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait")]
internal abstract class OpCompositeTrait : IOpTrait
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
    public abstract IOpTrait TraitAt(int index);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "getTraitDef()")]
    public abstract OpTraitDef TraitDef { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "satisfies(RelTrait)")]
    public abstract bool Satisfies(IOpTrait other);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "register(RelOptPlanner)")]
    public void Register(IOpPlanner planner)
    {
    }

}

/// <summary>
/// A trait that consists of a list of traits, all of the same dimension. It lets a
/// <see cref="OpTraitSet"/> hold several values on one dimension (e.g. several sort orders).
/// </summary>
/// <typeparam name="T">The member trait type.</typeparam>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait")]
internal class OpCompositeTrait<T> : OpCompositeTrait
    where T : class, IOpMultipleTrait
{

    readonly OpTraitDef _def;
    readonly ImmutableArray<T> _traits;

    OpCompositeTrait(OpTraitDef def, ImmutableArray<T> traits)
    {
        _def = def;
        _traits = traits;
        Debug.Assert(IsStrictlyOrdered(traits), "[" + string.Join(", ", traits) + "]");
        foreach (var trait in traits)
            Debug.Assert(ReferenceEquals(trait.TraitDef, def));
    }

    // Whether the members are strictly increasing by natural ordering — Calcite asserts the composite's
    // members are sorted and distinct via Ordering.natural().isStrictlyOrdered.
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Ordering", "isStrictlyOrdered(Iterable)")]
    static bool IsStrictlyOrdered(ImmutableArray<T> traits)
    {
        for (int i = 1; i < traits.Length; i++)
            if (traits[i - 1].CompareTo(traits[i]) >= 0)
                return false;

        return true;
    }

    /// <summary>
    /// Creates a trait from the members: the dimension's default when empty, the sole member when there
    /// is one, otherwise a composite.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "of(RelTraitDef, List)")]
    public static IOpTrait Of(OpTraitDef<T> def, IReadOnlyList<T> traits)
    {
        if (traits.Count == 0)
            return def.Default;
        if (traits.Count == 1)
            return def.Canonize(traits[0]);

        // Canonize each member, then the whole composite, so equal composites share one instance.
        var canonized = ImmutableArray.CreateBuilder<T>(traits.Count);
        foreach (var trait in traits)
            canonized.Add(def.Canonize(trait));

        return def.Canonize(new OpCompositeTrait<T>(def, canonized.MoveToImmutable()));
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
    public override IOpTrait TraitAt(int index) => _traits[index];

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "getTraitDef()")]
    public override OpTraitDef TraitDef => _def;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "satisfies(RelTrait)")]
    public override bool Satisfies(IOpTrait other)
    {
        return _traits.Any(t => t.Satisfies(other));
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelCompositeTrait", "equals(Object)")]
    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || (obj is OpCompositeTrait<T> other && _traits.SequenceEqual(other._traits));
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
