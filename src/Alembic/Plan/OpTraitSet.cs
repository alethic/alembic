using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace Alembic.Plan;

/// <summary>
/// An immutable, interned, ordered set of traits. A dimension is located by a linear scan over the
/// slots (there are few dimensions). Equal sets are canonicalized to a single shared instance through
/// a cache that every set derived from a common empty set shares, so an op holds one reference and
/// common sets are singletons.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet")]
public sealed class OpTraitSet : IEquatable<OpTraitSet>, IEnumerable<IOpTrait>
{

    readonly Cache _cache;
    readonly ImmutableArray<IOpTrait> _traits;
    int _hash;
    string? _string;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "RelTraitSet(Cache, RelTrait[])")]
    OpTraitSet(Cache cache, ImmutableArray<IOpTrait> traits)
    {
        _cache = cache;
        _traits = traits;
    }

    /// <summary>
    /// Creates an empty trait set. It starts a new cache, shared by every set derived from it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "createEmpty()")]
    public static OpTraitSet CreateEmpty()
    {
        return new OpTraitSet(new Cache(), ImmutableArray<IOpTrait>.Empty);
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
    public TTrait? Get<TTrait>(OpTraitDef<TTrait> def)
        where TTrait : class, IOpTrait
    {
        var index = FindIndex(def);
        return index < 0 ? null : (TTrait)_traits[index];
    }

    /// <summary>
    /// The value carried on the given dimension, or the dimension's default if it is not present.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "getTrait(RelTraitDef)")]
    public IOpTrait Get(OpTraitDef def)
    {
        var index = FindIndex(def);
        return index >= 0 ? _traits[index] : def.Default;
    }

    /// <summary>
    /// The trait carried at the given dimension index.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "getTrait(int)")]
    public IOpTrait Get(int index)
    {
        var trait = _traits[index];
        if (trait is OpCompositeTrait)
            throw new InvalidOperationException(
                "Trait index " + index + " has multiple values in this trait set; "
                + "use GetList(OpTraitDef) instead of Get(int)");

        return trait;
    }

    /// <summary>
    /// The number of dimensions in this set.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "size()")]
    public int Count => _traits.Length;

    /// <summary>
    /// Returns an interned set with the given trait added, or replaced if its dimension is already
    /// present.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "plus(RelTrait)")]
    public OpTraitSet Plus(IOpTrait trait)
    {
        if (Contains(trait))
            return this;

        var index = FindIndex(trait.TraitDef);
        if (index >= 0)
            return ReplaceAt(index, trait);

        return _cache.GetOrAdd(new OpTraitSet(_cache, _traits.Add(Canonize(trait))));
    }

    /// <summary>
    /// Returns the canonical (interned) instance equal to <paramref name="trait"/>, via its dimension.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "canonize(RelTrait)")]
    public IOpTrait Canonize(IOpTrait trait)
    {
        // Composite traits are canonized on creation (by OpCompositeTrait.Of), so return as-is.
        if (trait is OpCompositeTrait)
            return trait;

        return trait.TraitDef.Canonize(trait);
    }

    /// <summary>
    /// Returns an interned set identical to this one but with the given dimension replaced. If the
    /// dimension is not present, returns this set unchanged.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "replace(int, RelTrait)")]
    public OpTraitSet Replace<TTrait>(OpTraitDef<TTrait> def, TTrait value)
        where TTrait : class, IOpTrait
    {
        var index = FindIndex(def);
        if (index < 0)
            return this;

        return _cache.GetOrAdd(new OpTraitSet(_cache, _traits.SetItem(index, value)));
    }

    /// <summary>
    /// Returns an interned set carrying several values on one dimension at once. The values are folded
    /// into a single trait (the dimension's default when empty, the sole value when there is one, a
    /// <see cref="OpCompositeTrait{T}"/> otherwise) and added, or replaced if the dimension is present.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "replace(RelTraitDef, List)")]
    public OpTraitSet Replace<TTrait>(OpTraitDef<TTrait> def, IReadOnlyList<TTrait> values)
        where TTrait : class, IOpMultipleTrait
    {
        // Calcite delegates to replace(RelTrait): an absent dimension is ignored (returns this).
        return Replace(OpCompositeTrait<TTrait>.Of(def, values));
    }

    /// <summary>
    /// The values carried on a multi-valued dimension: the members of a <see cref="OpCompositeTrait{T}"/>
    /// if one is present, the single value if a plain trait is present, or empty if the dimension is
    /// absent.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "getTraits(RelTraitDef)")]
    public IReadOnlyList<TTrait> GetList<TTrait>(OpTraitDef<TTrait> def)
        where TTrait : class, IOpMultipleTrait
    {
        var index = FindIndex(def);
        if (index < 0)
            return Array.Empty<TTrait>();

        return _traits[index] is OpCompositeTrait<TTrait> composite
            ? composite.Traits
            : new[] { (TTrait)_traits[index] };
    }

    /// <summary>
    /// Whether an op carrying this set also satisfies a requirement for every dimension named in
    /// <paramref name="required"/>. A dimension absent from this set is never satisfied; present
    /// dimensions are compared with <see cref="IOpTrait.Satisfies"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "satisfies(RelTraitSet)")]
    public bool Satisfies(OpTraitSet required)
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
    public bool EqualsSansConvention(OpTraitSet other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (Count != other.Count)
            return false;

        for (int i = 0; i < _traits.Length; i++)
        {
            if (ReferenceEquals(_traits[i].TraitDef, ConventionTraitDef.Instance))
                continue;

            // Each trait is canonized already, so reference identity is value equality.
            if (!ReferenceEquals(_traits[i], other._traits[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Whether this set carries a trait equal to <paramref name="trait"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "contains(RelTrait)")]
    public bool Contains(IOpTrait trait)
    {
        foreach (var t in _traits)
            if (Equals(t, trait))
                return true;

        return false;
    }

    /// <summary>
    /// Whether the given dimension is present in this set.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "isEnabled(RelTraitDef)")]
    public bool IsEnabled<TTrait>(OpTraitDef<TTrait> def)
        where TTrait : class, IOpTrait
        => FindIndex(def) >= 0;

    /// <summary>
    /// Whether every dimension in this set holds its default value.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "isDefault()")]
    public bool IsDefault()
    {
        foreach (var trait in _traits)
            if (!ReferenceEquals(trait, trait.TraitDef.Default))
                return false;

        return true;
    }

    /// <summary>
    /// As <see cref="IsDefault"/>, ignoring the convention dimension.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "isDefaultSansConvention()")]
    public bool IsDefaultSansConvention()
    {
        foreach (var trait in _traits)
        {
            if (ReferenceEquals(trait.TraitDef, ConventionTraitDef.Instance))
                continue;

            if (!ReferenceEquals(trait, trait.TraitDef.Default))
                return false;
        }

        return true;
    }

    /// <summary>
    /// This set with every dimension reset to its default.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "getDefault()")]
    public OpTraitSet GetDefault()
    {
        var builder = ImmutableArray.CreateBuilder<IOpTrait>(_traits.Length);
        foreach (var trait in _traits)
            builder.Add(trait.TraitDef.Default);

        return _cache.GetOrAdd(new OpTraitSet(_cache, builder.MoveToImmutable()));
    }

    /// <summary>
    /// As <see cref="GetDefault"/>, but keeping the convention dimension unchanged.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "getDefaultSansConvention()")]
    public OpTraitSet GetDefaultSansConvention()
    {
        var builder = ImmutableArray.CreateBuilder<IOpTrait>(_traits.Length);
        foreach (var trait in _traits)
            builder.Add(ReferenceEquals(trait.TraitDef, ConventionTraitDef.Instance) ? trait : trait.TraitDef.Default);

        return _cache.GetOrAdd(new OpTraitSet(_cache, builder.MoveToImmutable()));
    }

    /// <summary>
    /// Whether <paramref name="trait"/> is carried here, or its dimension is absent (so it does not
    /// apply). A canonized trait is compared by reference.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "containsIfApplicable(RelTrait)")]
    public bool ContainsIfApplicable(IOpTrait trait)
    {
        var index = FindIndex(trait.TraitDef);
        if (index < 0)
            return true;

        return ReferenceEquals(_traits[index], trait);
    }

    /// <summary>
    /// Whether this set carries exactly <paramref name="traits"/>, in order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "comprises(RelTrait[])")]
    public bool Comprises(params IOpTrait[] traits)
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
    public bool Matches(OpTraitSet that)
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
    public IReadOnlyList<IOpTrait> Difference(OpTraitSet traitSet)
    {
        var builder = ImmutableArray.CreateBuilder<IOpTrait>();
        var n = Math.Min(_traits.Length, traitSet._traits.Length);
        for (int i = 0; i < n; i++)
            if (!Equals(_traits[i], traitSet._traits[i]))
                builder.Add(traitSet._traits[i]);

        return builder.ToImmutable();
    }

    /// <summary>
    /// Returns an interned set with the given trait substituted onto its dimension, <em>if that dimension
    /// is already present</em>; otherwise returns this set unchanged (the trait is ignored). Contrast with
    /// <see cref="Plus(IOpTrait)"/>, which adds an absent dimension.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "replace(RelTrait)")]
    public OpTraitSet Replace(IOpTrait trait)
    {
        // Quick check for the common case: the trait is already present by identity.
        if (ContainsShallow(trait))
            return this;

        var index = FindIndex(trait.TraitDef);
        if (index < 0)
            return this;

        return ReplaceAt(index, trait);
    }

    // Whether the (canonical) trait is present by reference identity — Calcite's containsShallow.
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "containsShallow(RelTrait[], RelTrait)")]
    bool ContainsShallow(IOpTrait seek)
    {
        foreach (var t in _traits)
            if (ReferenceEquals(t, seek))
                return true;

        return false;
    }

    /// <summary>
    /// Folds another set's traits into this one with <see cref="Plus(IOpTrait)"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "merge(RelTraitSet)")]
    public OpTraitSet Merge(OpTraitSet additionalTraits) => PlusAll(additionalTraits._traits);

    /// <summary>
    /// Adds each of <paramref name="traits"/> with <see cref="Plus(IOpTrait)"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "plusAll(RelTrait[])")]
    public OpTraitSet PlusAll(IEnumerable<IOpTrait> traits)
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
    public OpTraitSet ReplaceIfs<TTrait>(OpTraitDef<TTrait> def, Func<IReadOnlyList<TTrait>?> traitSupplier)
        where TTrait : class, IOpMultipleTrait
    {
        var index = FindIndex(def);
        if (index < 0)
            return this;

        var traits = traitSupplier();
        return ReplaceAt(index, traits is null ? def.Default : OpCompositeTrait<TTrait>.Of(def, traits));
    }

    /// <summary>
    /// If the given dimension is present, replaces it with the supplied value (or the dimension's default
    /// when the supplier returns <c>null</c>); otherwise returns this set unchanged.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "replaceIf(RelTraitDef, Supplier)")]
    public OpTraitSet ReplaceIf<TTrait>(OpTraitDef<TTrait> def, Func<TTrait?> traitSupplier)
        where TTrait : class, IOpTrait
    {
        var index = FindIndex(def);
        if (index < 0)
            return this;

        IOpTrait? value = traitSupplier();
        return ReplaceAt(index, value ?? def.Default);
    }

    /// <summary>
    /// Whether this set carries no <see cref="OpCompositeTrait{T}"/> (every dimension holds a single value).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "allSimple()")]
    public bool AllSimple()
    {
        foreach (var trait in _traits)
            if (trait is OpCompositeTrait)
                return false;

        return true;
    }

    /// <summary>
    /// An interned set like this one but with every composite trait flattened: a one-member composite
    /// becomes that member, a many-member composite becomes its dimension's default.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "simplify()")]
    public OpTraitSet Simplify()
    {
        var result = this;
        for (int i = 0; i < _traits.Length; i++)
            if (_traits[i] is OpCompositeTrait composite)
                result = result.ReplaceAt(i, composite.Count == 1 ? composite.TraitAt(0) : _traits[i].TraitDef.Default);

        return result;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "iterator()")]
    public IEnumerator<IOpTrait> GetEnumerator() => ((IEnumerable<IOpTrait>)_traits).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "replace(int, RelTrait)")]
    OpTraitSet ReplaceAt(int index, IOpTrait trait)
    {
        Debug.Assert(ReferenceEquals(_traits[index].TraitDef, trait.TraitDef),
            "trait has a different trait def than its replacement");

        trait = Canonize(trait);
        if (ReferenceEquals(_traits[index], trait))
            return this;

        return _cache.GetOrAdd(new OpTraitSet(_cache, _traits.SetItem(index, trait)));
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "findIndex(RelTraitDef)")]
    int FindIndex(OpTraitDef def)
    {
        for (int i = 0; i < _traits.Length; i++)
            if (ReferenceEquals(_traits[i].TraitDef, def))
                return i;

        return -1;
    }

    /// <inheritdoc />
    public bool Equals(OpTraitSet? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null)
            return false;

        // If both hashes are computed and differ, the sets differ.
        if (_hash != 0 && other._hash != 0 && _hash != other._hash)
            return false;
        if (_traits.Length != other._traits.Length)
            return false;

        // Each trait is canonized, so reference identity is value equality.
        for (int i = 0; i < _traits.Length; i++)
            if (!ReferenceEquals(_traits[i], other._traits[i]))
                return false;

        return true;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "equals(Object)")]
    public override bool Equals(object? obj)
    {
        return obj is OpTraitSet other && Equals(other);
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
    public override string ToString() => _string ??= ComputeString();

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet", "computeString()")]
    string ComputeString()
    {
        var s = new StringBuilder();
        for (int i = 0; i < _traits.Length; i++)
        {
            if (i > 0)
                s.Append('.');

            s.Append(_traits[i]);
        }

        return s.ToString();
    }

    /// <summary>
    /// The intern cache for one ancestral line of trait sets.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitSet.Cache")]
    sealed class Cache
    {

        readonly Dictionary<OpTraitSet, OpTraitSet> _map = new Dictionary<OpTraitSet, OpTraitSet>();

        public OpTraitSet GetOrAdd(OpTraitSet set)
        {
            if (_map.TryGetValue(set, out var existing))
                return existing;

            _map[set] = set;
            return set;
        }

    }

}
