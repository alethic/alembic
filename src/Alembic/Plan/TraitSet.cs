using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Alembic.Plan;

/// <summary>
/// An immutable, interned, ordered set of traits. A dimension is located by a linear scan over the
/// slots (there are few dimensions). Equal sets are canonicalized to a single shared instance through
/// a cache that every set derived from a common empty set shares, so a node holds one reference and
/// common sets are singletons.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet")]
public sealed class TraitSet : IEquatable<TraitSet>, IEnumerable<ITrait>
{

    readonly Cache _cache;
    readonly ImmutableArray<ITrait> _traits;
    int _hash;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "RelTraitSet(Cache, RelTrait[])")]
    TraitSet(Cache cache, ImmutableArray<ITrait> traits)
    {
        _cache = cache;
        _traits = traits;
    }

    /// <summary>
    /// Creates an empty trait set. It starts a new cache, shared by every set derived from it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "createEmpty()")]
    public static TraitSet CreateEmpty()
    {
        var cache = new Cache();
        return cache.GetOrAdd(new TraitSet(cache, ImmutableArray<ITrait>.Empty));
    }

    /// <summary>
    /// The convention carried by this set (every set carries one).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "getConvention()")]
    public IConvention Convention => Get(ConventionTraitDef.Instance)!;

    /// <summary>
    /// The value carried on the given dimension, or <c>null</c> if the dimension is not present.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "getTrait(RelTraitDef)")]
    public TTrait? Get<TTrait>(TraitDef<TTrait> def)
        where TTrait : class, ITrait
    {
        var index = FindIndex(def);
        return index < 0 ? null : (TTrait)_traits[index];
    }

    /// <summary>
    /// The value carried on the given dimension, or the dimension's default if it is not present.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "getTrait(RelTraitDef)")]
    public ITrait Get(TraitDef def)
    {
        var index = FindIndex(def);
        return index >= 0 ? _traits[index] : def.Default;
    }

    /// <summary>
    /// Returns an interned set with the given trait added, or replaced if its dimension is already
    /// present.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "plus(RelTrait)")]
    public TraitSet Plus(ITrait trait)
    {
        trait = Canonize(trait);
        var index = FindIndex(trait.TraitDef);
        var next = index >= 0 ? _traits.SetItem(index, trait) : _traits.Add(trait);
        return _cache.GetOrAdd(new TraitSet(_cache, next));
    }

    /// <summary>
    /// Returns the canonical (interned) instance equal to <paramref name="trait"/>, via its dimension.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "canonize(RelTrait)")]
    public ITrait Canonize(ITrait trait)
    {
        return trait.TraitDef.Canonize(trait);
    }

    /// <summary>
    /// Returns an interned set identical to this one but with the given dimension replaced. If the
    /// dimension is not present, returns this set unchanged.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "replace(int, RelTrait)")]
    public TraitSet Replace<TTrait>(TraitDef<TTrait> def, TTrait value)
        where TTrait : class, ITrait
    {
        var index = FindIndex(def);
        if (index < 0)
            return this;

        return _cache.GetOrAdd(new TraitSet(_cache, _traits.SetItem(index, value)));
    }

    /// <summary>
    /// Returns an interned set carrying several values on one dimension at once. The values are folded
    /// into a single trait (the dimension's default when empty, the sole value when there is one, a
    /// <see cref="CompositeTrait{T}"/> otherwise) and added, or replaced if the dimension is present.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "replace(RelTraitDef, List)")]
    public TraitSet Replace<TTrait>(TraitDef<TTrait> def, IReadOnlyList<TTrait> values)
        where TTrait : class, IMultipleTrait
    {
        var trait = CompositeTrait<TTrait>.Of(def, values);
        var index = FindIndex(def);
        var next = index >= 0 ? _traits.SetItem(index, trait) : _traits.Add(trait);
        return _cache.GetOrAdd(new TraitSet(_cache, next));
    }

    /// <summary>
    /// The values carried on a multi-valued dimension: the members of a <see cref="CompositeTrait{T}"/>
    /// if one is present, the single value if a plain trait is present, or empty if the dimension is
    /// absent.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "getTraits(RelTraitDef)")]
    public IReadOnlyList<TTrait> GetList<TTrait>(TraitDef<TTrait> def)
        where TTrait : class, IMultipleTrait
    {
        var index = FindIndex(def);
        if (index < 0)
            return Array.Empty<TTrait>();

        return _traits[index] is CompositeTrait<TTrait> composite
            ? composite.Traits
            : new[] { (TTrait)_traits[index] };
    }

    /// <summary>
    /// Whether a node carrying this set also satisfies a requirement for every dimension named in
    /// <paramref name="required"/>. A dimension absent from this set is never satisfied; present
    /// dimensions are compared with <see cref="ITrait.Satisfies"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "satisfies(RelTraitSet)")]
    public bool Satisfies(TraitSet required)
    {
        foreach (var requirement in required._traits)
        {
            var index = FindIndex(requirement.TraitDef);
            if (index < 0 || !_traits[index].Satisfies(requirement))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Whether this set equals <paramref name="other"/> on every dimension except convention.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "equalsSansConvention(RelTraitSet)")]
    public bool EqualsSansConvention(TraitSet other)
    {
        return Replace(ConventionTraitDef.Instance, other.Convention).Equals(other);
    }

    /// <summary>
    /// Whether this set carries a trait equal to <paramref name="trait"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "contains(RelTrait)")]
    public bool Contains(ITrait trait)
    {
        foreach (var t in _traits)
            if (Equals(t, trait))
                return true;

        return false;
    }

    /// <summary>
    /// Whether this set carries exactly <paramref name="traits"/>, in order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "comprises(RelTrait[])")]
    public bool Comprises(params ITrait[] traits)
    {
        if (traits.Length != _traits.Length)
            return false;

        for (int i = 0; i < _traits.Length; i++)
            if (!Equals(_traits[i], traits[i]))
                return false;

        return true;
    }

    /// <summary>
    /// Whether this set matches <paramref name="that"/> exactly on every dimension both define (a
    /// stronger, equality-based counterpart to <see cref="Satisfies"/>).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "matches(RelTraitSet)")]
    public bool Matches(TraitSet that)
    {
        var n = Math.Min(_traits.Length, that._traits.Length);
        for (int i = 0; i < n; i++)
            if (!Equals(_traits[i], that._traits[i]))
                return false;

        return true;
    }

    /// <summary>
    /// The traits in <paramref name="traitSet"/> that differ, position for position, from this set's —
    /// i.e. the dimensions on which the two disagree, taken from <paramref name="traitSet"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "difference(RelTraitSet)")]
    public IReadOnlyList<ITrait> Difference(TraitSet traitSet)
    {
        var builder = ImmutableArray.CreateBuilder<ITrait>();
        var n = Math.Min(_traits.Length, traitSet._traits.Length);
        for (int i = 0; i < n; i++)
            if (!Equals(_traits[i], traitSet._traits[i]))
                builder.Add(traitSet._traits[i]);

        return builder.ToImmutable();
    }

    /// <summary>
    /// Returns an interned set with the given trait substituted onto its dimension, <em>if that dimension
    /// is already present</em>; otherwise returns this set unchanged (the trait is ignored). Contrast with
    /// <see cref="Plus(ITrait)"/>, which adds an absent dimension.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "replace(RelTrait)")]
    public TraitSet Replace(ITrait trait)
    {
        trait = Canonize(trait);
        var index = FindIndex(trait.TraitDef);
        if (index < 0)
            return this;

        return ReplaceAt(index, trait);
    }

    /// <summary>
    /// Folds another set's traits into this one with <see cref="Plus(ITrait)"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "merge(RelTraitSet)")]
    public TraitSet Merge(TraitSet additionalTraits) => PlusAll(additionalTraits._traits);

    /// <summary>
    /// Adds each of <paramref name="traits"/> with <see cref="Plus(ITrait)"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "plusAll(RelTrait[])")]
    public TraitSet PlusAll(IEnumerable<ITrait> traits)
    {
        var result = this;
        foreach (var trait in traits)
            result = result.Plus(trait);

        return result;
    }

    /// <summary>
    /// If the given multi-valued dimension is present, replaces it with the supplied values (or the
    /// dimension's default when the supplier returns <c>null</c>); otherwise returns this set unchanged.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "replaceIfs(RelTraitDef, Supplier)")]
    public TraitSet ReplaceIfs<TTrait>(TraitDef<TTrait> def, Func<IReadOnlyList<TTrait>?> traitSupplier)
        where TTrait : class, IMultipleTrait
    {
        var index = FindIndex(def);
        if (index < 0)
            return this;

        var traits = traitSupplier();
        return ReplaceAt(index, traits is null ? def.Default : CompositeTrait<TTrait>.Of(def, traits));
    }

    /// <summary>
    /// If the given dimension is present, replaces it with the supplied value (or the dimension's default
    /// when the supplier returns <c>null</c>); otherwise returns this set unchanged.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "replaceIf(RelTraitDef, Supplier)")]
    public TraitSet ReplaceIf<TTrait>(TraitDef<TTrait> def, Func<TTrait?> traitSupplier)
        where TTrait : class, ITrait
    {
        var index = FindIndex(def);
        if (index < 0)
            return this;

        ITrait? value = traitSupplier();
        return ReplaceAt(index, value ?? def.Default);
    }

    /// <summary>
    /// Whether this set carries no <see cref="CompositeTrait{T}"/> (every dimension holds a single value).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "allSimple()")]
    public bool AllSimple()
    {
        foreach (var trait in _traits)
            if (trait is CompositeTrait)
                return false;

        return true;
    }

    /// <summary>
    /// An interned set like this one but with every composite trait flattened: a one-member composite
    /// becomes that member, a many-member composite becomes its dimension's default.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "simplify()")]
    public TraitSet Simplify()
    {
        var result = this;
        for (int i = 0; i < _traits.Length; i++)
            if (_traits[i] is CompositeTrait composite)
                result = result.ReplaceAt(i, composite.Count == 1 ? composite.TraitAt(0) : _traits[i].TraitDef.Default);

        return result;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "iterator()")]
    public IEnumerator<ITrait> GetEnumerator() => ((IEnumerable<ITrait>)_traits).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "replace(int, RelTrait)")]
    TraitSet ReplaceAt(int index, ITrait trait)
    {
        trait = Canonize(trait);
        if (ReferenceEquals(_traits[index], trait))
            return this;

        return _cache.GetOrAdd(new TraitSet(_cache, _traits.SetItem(index, trait)));
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "findIndex(RelTraitDef)")]
    int FindIndex(TraitDef def)
    {
        for (int i = 0; i < _traits.Length; i++)
            if (ReferenceEquals(_traits[i].TraitDef, def))
                return i;

        return -1;
    }

    /// <inheritdoc />
    public bool Equals(TraitSet? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null || other._traits.Length != _traits.Length)
            return false;

        for (int i = 0; i < _traits.Length; i++)
            if (!Equals(_traits[i], other._traits[i]))
                return false;

        return true;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "equals(Object)")]
    public override bool Equals(object? obj)
    {
        return obj is TraitSet other && Equals(other);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "hashCode()")]
    public override int GetHashCode()
    {
        if (_hash == 0)
        {
            var h = new HashCode();
            foreach (var t in _traits)
                h.Add(t);

            _hash = h.ToHashCode();
            if (_hash == 0)
                _hash = 1;
        }

        return _hash;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "toString()")]
    public override string ToString() => "[" + string.Join(", ", _traits) + "]";

    /// <summary>
    /// The intern cache for one ancestral line of trait sets.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet.Cache")]
    sealed class Cache
    {

        readonly Dictionary<TraitSet, TraitSet> _map = new Dictionary<TraitSet, TraitSet>();

        public TraitSet GetOrAdd(TraitSet set)
        {
            if (_map.TryGetValue(set, out var existing))
                return existing;

            _map[set] = set;
            return set;
        }

    }

}
