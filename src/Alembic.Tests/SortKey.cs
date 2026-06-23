using System;

using Alembic.Plan;

namespace Alembic.Tests;

/// <summary>
/// A toy multi-valued trait: a single sort key. A node may carry several at once (sorted by more than
/// one key), so several can live on one dimension via a composite.
/// </summary>
sealed class SortKey : IMultipleTrait
{

    public static readonly SortKey None = new SortKey("");

    readonly string _key;

    public SortKey(string key)
    {
        _key = key;
    }

    public string Key => _key;

    TraitDef ITrait.TraitDef => SortKeyTraitDef.Instance;

    public bool IsTop => _key.Length == 0;

    public bool Satisfies(ITrait other)
    {
        return other is SortKey key && (key._key.Length == 0 || string.Equals(key._key, _key, StringComparison.Ordinal));
    }

    public int CompareTo(IMultipleTrait? other)
    {
        return string.CompareOrdinal(_key, (other as SortKey)?._key ?? string.Empty);
    }

    public override bool Equals(object? obj)
    {
        return obj is SortKey other && string.Equals(_key, other._key, StringComparison.Ordinal);
    }

    public override int GetHashCode() => _key.GetHashCode();

    public override string ToString() => _key;

}
