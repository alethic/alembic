using System.Collections.Generic;

namespace Alembic.Util;

/// <summary>
/// A collection that maps each key to a collection of values — the .NET stand-in for Guava's
/// <c>HashMultimap</c>, backed by a dictionary of lists. The same key may be associated with a value more
/// than once.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
[Provenance(ProvenanceSource.Other, "com.google.common.collect.HashMultimap")]
public sealed class Multimap<TKey, TValue>
    where TKey : notnull
{

    readonly Dictionary<TKey, List<TValue>> _map = new Dictionary<TKey, List<TValue>>();

    /// <summary>
    /// Associates <paramref name="value"/> with <paramref name="key"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Multimap", "put(K, V)")]
    public void Put(TKey key, TValue value)
    {
        if (!_map.TryGetValue(key, out var values))
            _map[key] = values = new List<TValue>();

        values.Add(value);
    }

    /// <summary>
    /// Removes a single <paramref name="key"/>→<paramref name="value"/> association, returning whether one
    /// was present.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Multimap", "remove(Object, Object)")]
    public bool Remove(TKey key, TValue value)
    {
        return _map.TryGetValue(key, out var values) && values.Remove(value);
    }

    /// <summary>
    /// The values associated with <paramref name="key"/>, or an empty collection if there are none.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Multimap", "get(K)")]
    public IReadOnlyList<TValue> Get(TKey key)
    {
        return _map.TryGetValue(key, out var values) ? values : System.Array.Empty<TValue>();
    }

    /// <summary>
    /// Removes and returns every value associated with <paramref name="key"/>, leaving no entry for it.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Multimap", "removeAll(Object)")]
    public IReadOnlyList<TValue> RemoveAll(TKey key)
    {
        return _map.Remove(key, out var values) ? values : System.Array.Empty<TValue>();
    }

    /// <summary>
    /// Removes every value (across all keys) matching <paramref name="predicate"/>, pruning any key left
    /// with no values. The .NET stand-in for <c>values().removeIf(predicate)</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Multimap", "values()")]
    public void RemoveValuesWhere(System.Func<TValue, bool> predicate)
    {
        foreach (var key in new List<TKey>(_map.Keys))
        {
            var values = _map[key];
            values.RemoveAll(v => predicate(v));
            if (values.Count == 0)
                _map.Remove(key);
        }
    }

    /// <summary>
    /// Removes every key→value association.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Multimap", "clear()")]
    public void Clear() => _map.Clear();

}
