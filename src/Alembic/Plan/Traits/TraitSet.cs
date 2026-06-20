using System;
using System.Collections.Immutable;

namespace Alembic.Plan.Traits;

/// <summary>
/// An immutable, interned set of traits — one slot per registered dimension, indexed by ordinal.
/// Built through a <see cref="TraitContext"/>, which canonicalizes equal sets so that a node holds
/// a single shared reference.
/// </summary>
public sealed class TraitSet : IEquatable<TraitSet>
{

    readonly TraitContext _context;
    readonly ImmutableArray<ITrait> _traits;
    int _hash;

    internal TraitSet(TraitContext context, ImmutableArray<ITrait> traits)
    {
        _context = context;
        _traits = traits;
    }

    /// <summary>
    /// The context that owns this set's dimensions.
    /// </summary>
    public TraitContext Context => _context;

    /// <summary>
    /// The convention carried by this set.
    /// </summary>
    public Convention Convention => Get(ConventionTraitDef.Instance);

    /// <summary>
    /// The value carried on the given dimension.
    /// </summary>
    public TTrait Get<TTrait>(TraitDef<TTrait> def)
        where TTrait : ITrait
    {
        return (TTrait)_traits[_context.Ordinal(def)];
    }

    /// <summary>
    /// Returns an interned set identical to this one but with the given dimension replaced.
    /// </summary>
    public TraitSet Replace<TTrait>(TraitDef<TTrait> def, TTrait value)
        where TTrait : ITrait
    {
        var next = _traits.SetItem(_context.Ordinal(def), value);
        return _context.Intern(new TraitSet(_context, next));
    }

    /// <inheritdoc />
    public bool Equals(TraitSet? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || other._traits.Length != _traits.Length) return false;

        for (int i = 0; i < _traits.Length; i++)
        {
            if (!Equals(_traits[i], other._traits[i])) return false;
        }

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
            if (_hash == 0) _hash = 1;
        }

        return _hash;
    }

}
