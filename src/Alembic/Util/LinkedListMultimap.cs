using System;
using System.Collections.Generic;

namespace Alembic.Util;

/// <summary>
/// A multimap that keeps every key→value entry in a single doubly-linked chain (preserving overall
/// insertion order) with a per-key index into that chain (preserving per-key insertion order). A given
/// key→value pair may be stored more than once. The .NET stand-in for Guava's <c>LinkedListMultimap</c>,
/// whose <c>Node</c>s are linked both globally (<c>head</c>/<c>tail</c> + <c>next</c>/<c>previous</c>) and
/// per key (<c>nextSibling</c>/<c>previousSibling</c>, indexed by <c>keyToKeyList</c>).
/// </summary>
/// <remarks>
/// Guava's <c>get</c> and <c>values</c> hand back live, mutate-through sequential-list views; Alembic only
/// reads them, so <c>Get</c> returns a snapshot and <c>RemoveValuesWhere</c> drives the removal directly.
/// <c>size</c>/<c>modCount</c> are not ported (no <c>size()</c> or fail-fast iteration consumer).
/// </remarks>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
[Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap")]
public sealed class LinkedListMultimap<TKey, TValue>
    where TKey : notnull
{

    // The global chain of all entries in insertion order (= Guava's head/tail + Node.next/previous).
    readonly LinkedList<KeyValuePair<TKey, TValue>> _entries = new();

    // Per-key index into the global chain, in per-key insertion order (= Guava's keyToKeyList + the
    // Node.nextSibling/previousSibling chain).
    readonly Dictionary<TKey, List<LinkedListNode<KeyValuePair<TKey, TValue>>>> _index = new();

    /// <summary>
    /// Stores <paramref name="value"/> with <paramref name="key"/>, appending a node to both the overall
    /// chain and the key's chain (= Guava's <c>addNode(key, value, null)</c>). Always returns <c>true</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap", "put(K, V)")]
    public bool Put(TKey key, TValue value)
    {
        var node = _entries.AddLast(new KeyValuePair<TKey, TValue>(key, value));
        if (!_index.TryGetValue(key, out var nodes))
            _index[key] = nodes = new List<LinkedListNode<KeyValuePair<TKey, TValue>>>();

        nodes.Add(node);
        return true;
    }

    /// <summary>
    /// The values associated with <paramref name="key"/>, in insertion order (an empty list if none) — a
    /// snapshot, in lieu of Guava's live sequential-list view.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap", "get(K)")]
    public IReadOnlyList<TValue> Get(TKey key)
    {
        if (!_index.TryGetValue(key, out var nodes))
            return Array.Empty<TValue>();

        var values = new List<TValue>(nodes.Count);
        foreach (var node in nodes)
            values.Add(node.Value.Value);

        return values;
    }

    /// <summary>
    /// Removes every value (across all keys) matching <paramref name="predicate"/>, walking the overall
    /// chain in insertion order and pruning any key left with no values — the .NET stand-in for
    /// <c>values().removeIf(predicate)</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap", "values()")]
    public void RemoveValuesWhere(Func<TValue, bool> predicate)
    {
        for (var node = _entries.First; node is not null;)
        {
            var next = node.Next;
            if (predicate(node.Value.Value))
            {
                var key = node.Value.Key;
                _entries.Remove(node);

                var nodes = _index[key];
                nodes.Remove(node);
                if (nodes.Count == 0)
                    _index.Remove(key);
            }

            node = next;
        }
    }

    /// <summary>
    /// Removes every key→value association.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap", "clear()")]
    public void Clear()
    {
        _entries.Clear();
        _index.Clear();
    }

}
