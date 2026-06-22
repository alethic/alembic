using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Alembic.Plan;

/// <summary>
/// An immutable, interned, ordered set of traits. A dimension is located by a linear scan over the
/// slots (there are few dimensions). Equal sets are canonicalized to a single shared instance through
/// a cache that every set derived from a common empty set shares, so a node holds one reference and
/// common sets are singletons.
/// </summary>
public sealed class TraitSet : IEquatable<TraitSet>
{

    readonly Cache _cache;
    readonly ImmutableArray<ITrait> _traits;
    int _hash;

    TraitSet(Cache cache, ImmutableArray<ITrait> traits)
    {
        _cache = cache;
        _traits = traits;
    }

    /// <summary>
    /// Creates an empty trait set. It starts a new cache, shared by every set derived from it.
    /// </summary>
    public static TraitSet CreateEmpty()
    {
        var cache = new Cache();
        return cache.GetOrAdd(new TraitSet(cache, ImmutableArray<ITrait>.Empty));
    }

    /// <summary>
    /// The convention carried by this set.
    /// </summary>
    public IConvention Convention => Get(ConventionTraitDef.Instance);

    /// <summary>
    /// The value carried on the given dimension.
    /// </summary>
    public TTrait Get<TTrait>(TraitDef<TTrait> def)
        where TTrait : ITrait
    {
        return (TTrait)_traits[FindIndex(def)];
    }

    /// <summary>
    /// The value carried on the given dimension, or the dimension's default if it is not present.
    /// </summary>
    public ITrait Get(ITraitDef def)
    {
        var index = FindIndex(def);
        return index >= 0 ? _traits[index] : def.Default;
    }

    /// <summary>
    /// Returns an interned set with the given trait added, or replaced if its dimension is already
    /// present.
    /// </summary>
    public TraitSet Plus(ITrait trait)
    {
        var index = FindIndex(trait.Def);
        var next = index >= 0 ? _traits.SetItem(index, trait) : _traits.Add(trait);
        return _cache.GetOrAdd(new TraitSet(_cache, next));
    }

    /// <summary>
    /// Returns an interned set identical to this one but with the given dimension replaced. If the
    /// dimension is not present, returns this set unchanged.
    /// </summary>
    public TraitSet Replace<TTrait>(TraitDef<TTrait> def, TTrait value)
        where TTrait : ITrait
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
    public TraitSet Replace<TTrait>(TraitDef<TTrait> def, IReadOnlyList<TTrait> values)
        where TTrait : IMultipleTrait
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
    public IReadOnlyList<TTrait> GetList<TTrait>(TraitDef<TTrait> def)
        where TTrait : IMultipleTrait
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
    public bool Satisfies(TraitSet required)
    {
        foreach (var requirement in required._traits)
        {
            var index = FindIndex(requirement.Def);
            if (index < 0 || !_traits[index].Satisfies(requirement))
                return false;
        }

        return true;
    }

    int FindIndex(ITraitDef def)
    {
        for (int i = 0; i < _traits.Length; i++)
            if (ReferenceEquals(_traits[i].Def, def))
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
    public override bool Equals(object? obj)
    {
        return obj is TraitSet other && Equals(other);
    }

    /// <inheritdoc />
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
    public override string ToString() => "[" + string.Join(", ", _traits) + "]";

    /// <summary>
    /// The intern cache for one ancestral line of trait sets.
    /// </summary>
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
