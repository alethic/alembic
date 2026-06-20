using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Alembic.Plan.Traits;

/// <summary>
/// Owns the registered trait dimensions (assigning each an ordinal) and the intern cache for
/// <see cref="TraitSet"/>. Equal trait sets are canonicalized to a single shared instance, so a
/// node's traits cost one reference and the common sets are shared singletons.
/// </summary>
public sealed class TraitContext
{

    readonly List<ITraitDef> _defs = new List<ITraitDef>();
    readonly Dictionary<ITraitDef, int> _ordinals = new Dictionary<ITraitDef, int>();
    readonly Dictionary<TraitSet, TraitSet> _interned = new Dictionary<TraitSet, TraitSet>();

    /// <summary>
    /// Creates a context with the convention dimension registered.
    /// </summary>
    public TraitContext()
    {
        Register(ConventionTraitDef.Instance);
        Empty = Intern(new TraitSet(this, _defs.Select(d => d.Default).ToImmutableArray()));
    }

    /// <summary>
    /// The trait set in which every dimension carries its default.
    /// </summary>
    public TraitSet Empty { get; }

    /// <summary>
    /// Registers a trait dimension, returning its ordinal (idempotent).
    /// </summary>
    public int Register(ITraitDef def)
    {
        if (_ordinals.TryGetValue(def, out var existing))
            return existing;

        var ordinal = _defs.Count;
        _defs.Add(def);
        _ordinals[def] = ordinal;
        return ordinal;
    }

    /// <summary>
    /// The ordinal of a registered dimension.
    /// </summary>
    public int Ordinal(ITraitDef def)
    {
        return _ordinals[def];
    }

    internal TraitSet Intern(TraitSet set)
    {
        if (_interned.TryGetValue(set, out var existing))
            return existing;

        _interned[set] = set;
        return set;
    }

}
