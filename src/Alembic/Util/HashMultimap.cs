using System;
using System.Collections.Generic;

namespace Alembic.Util;

/// <summary>
/// A multimap whose values for each key are held in a hash set, so a given key→value pair is stored at
/// most once and the values for a key are unordered. The .NET stand-in for Guava's <c>HashMultimap</c> —
/// an <c>AbstractSetMultimap</c> backed by a <c>HashMap&lt;K, HashSet&lt;V&gt;&gt;</c>, where the value
/// methods are implemented in <c>AbstractMapBasedMultimap</c>.
/// </summary>
/// <remarks>
/// Guava's <c>get</c>/<c>removeAll</c> hand back live, mutate-through collection views
/// (<c>WrappedCollection</c>); Alembic only ever reads them, so this renders them as direct (read-only)
/// access to the backing set instead of porting the view machinery. <c>size()</c> is not ported, so
/// Guava's <c>totalSize</c> bookkeeping is omitted from the value methods.
/// </remarks>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
[Provenance(ProvenanceSource.Other, "com.google.common.collect.HashMultimap")]
public sealed class HashMultimap<TKey, TValue>
    where TKey : notnull
{

    static readonly HashSet<TValue> Empty = new HashSet<TValue>();

    readonly Dictionary<TKey, HashSet<TValue>> _map = new Dictionary<TKey, HashSet<TValue>>();

    /// <summary>
    /// Stores <paramref name="value"/> with <paramref name="key"/>, returning whether the multimap changed
    /// (i.e. the pair was not already present).
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.AbstractMapBasedMultimap", "put(K, V)")]
    public bool Put(TKey key, TValue value)
    {
        if (!_map.TryGetValue(key, out var collection))
        {
            // createCollection() = HashMultimap.newHashSetWithExpectedSize(DEFAULT_VALUES_PER_KEY = 2).
            collection = new HashSet<TValue>(2);
            if (collection.Add(value))
            {
                _map[key] = collection;
                return true;
            }

            throw new InvalidOperationException("New Collection violated the Collection spec");
        }

        return collection.Add(value);
    }

    /// <summary>
    /// The set of values associated with <paramref name="key"/> (an empty set if there are none) — a
    /// read-only view of the backing set, in lieu of Guava's mutate-through <c>WrappedCollection</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.AbstractMapBasedMultimap", "get(K)")]
    public IReadOnlySet<TValue> Get(TKey key)
    {
        return _map.TryGetValue(key, out var collection) ? collection : Empty;
    }

    /// <summary>
    /// Removes a single <paramref name="key"/>→<paramref name="value"/> association, returning whether one
    /// was present. Prunes the key if it has no values left.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.AbstractMapBasedMultimap", "remove(Object, Object)")]
    public bool Remove(TKey key, TValue value)
    {
        if (!_map.TryGetValue(key, out var collection))
            return false;

        var changed = collection.Remove(value);
        if (changed && collection.Count == 0)
            _map.Remove(key);

        return changed;
    }

    /// <summary>
    /// Removes and returns every value associated with <paramref name="key"/>, leaving no entry for it.
    /// </summary>
    /// <remarks>
    /// Guava additionally copies the values into a fresh collection and clears the original (to empty any
    /// live view a caller still holds); since Alembic hands out no such views, this returns the removed set
    /// directly — it is already detached from the map.
    /// </remarks>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.AbstractMapBasedMultimap", "removeAll(Object)")]
    public IReadOnlyCollection<TValue> RemoveAll(TKey key)
    {
        return _map.Remove(key, out var collection) ? collection : Array.Empty<TValue>();
    }

    /// <summary>
    /// Removes every key→value association.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.AbstractMapBasedMultimap", "clear()")]
    public void Clear()
    {
        // Clear each collection first, to empty any view previously returned by Get.
        foreach (var collection in _map.Values)
            collection.Clear();

        _map.Clear();
    }

}
