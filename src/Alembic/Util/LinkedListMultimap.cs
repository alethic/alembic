using System;
using System.Collections;
using System.Collections.Generic;

namespace Alembic.Util;

/// <summary>
/// A multimap that keeps every key→value entry in a single doubly-linked chain (preserving overall
/// insertion order) with a per-key index into that chain (preserving per-key insertion order). A given
/// key→value pair may be stored more than once. The .NET stand-in for Guava's <c>LinkedListMultimap</c>,
/// whose <c>Node</c>s are linked both globally (<c>head</c>/<c>tail</c> + <c>next</c>/<c>previous</c>) and
/// per key (<c>nextSibling</c>/<c>previousSibling</c>, indexed by <c>keyToKeyList</c>). Here a
/// <see cref="LinkedList{T}"/> supplies the global chain (it is a head/tail doubly-linked list internally)
/// and a per-key list of its nodes supplies the per-key index.
/// </summary>
/// <remarks>
/// As in Guava, <see cref="Get"/> hands back a live, mutate-through view over the key's values rather than
/// a snapshot: reads reflect the current state and add/insert/remove/set change the multimap in place.
/// <c>values().removeIf(...)</c> is exposed narrowly as <see cref="RemoveValuesWhere"/> (Alembic never
/// iterates the whole value collection). <c>size</c>/<c>modCount</c> are not ported (no <c>size()</c>
/// consumer, and no live view is iterated concurrently with mutation — callers copy first).
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
    /// A live, mutate-through view of the values associated with <paramref name="key"/>, in insertion
    /// order — Guava's <c>get(K)</c> (an <c>AbstractSequentialList</c> backed by the key's sibling chain).
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap", "get(K)")]
    public IList<TValue> Get(TKey key) => new ValuesForKey(this, key);

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

    /// <summary>
    /// The live view returned by <see cref="Get"/> — the .NET equivalent of Guava's <c>get(K)</c> view and
    /// its <c>ValueForKeyIterator</c>. It holds only the key, resolving the key's node list on each access,
    /// so it reflects concurrent additions/removals; mutations insert or remove nodes in both the global
    /// chain (<see cref="_entries"/>) and the per-key index (<see cref="_index"/>), preserving global
    /// insertion order (a positional insert splices before the node currently at that position).
    /// </summary>
    sealed class ValuesForKey : IList<TValue>
    {

        readonly LinkedListMultimap<TKey, TValue> _map;
        readonly TKey _key;

        internal ValuesForKey(LinkedListMultimap<TKey, TValue> map, TKey key)
        {
            _map = map;
            _key = key;
        }

        List<LinkedListNode<KeyValuePair<TKey, TValue>>>? Nodes()
            => _map._index.GetValueOrDefault(_key);

        public int Count => Nodes()?.Count ?? 0;

        public bool IsReadOnly => false;

        public TValue this[int index]
        {
            get
            {
                var nodes = Nodes();
                if (nodes is null || (uint)index >= (uint)nodes.Count)
                    throw OutOfRange(index);

                return nodes[index].Value.Value;
            }
            set
            {
                var nodes = Nodes();
                if (nodes is null || (uint)index >= (uint)nodes.Count)
                    throw OutOfRange(index);

                nodes[index].Value = new KeyValuePair<TKey, TValue>(_key, value);
            }
        }

        public void Add(TValue value) => _map.Put(_key, value);

        public void Insert(int index, TValue value)
        {
            var nodes = Nodes();
            int count = nodes?.Count ?? 0;
            if ((uint)index > (uint)count)
                throw OutOfRange(index);

            if (index == count)
            {
                Add(value);
                return;
            }

            // Splice a node before the one currently at `index`, in both the global and per-key chains.
            var nextSibling = nodes![index];
            var node = _map._entries.AddBefore(nextSibling, new KeyValuePair<TKey, TValue>(_key, value));
            nodes.Insert(index, node);
        }

        public void RemoveAt(int index)
        {
            var nodes = Nodes();
            if (nodes is null || (uint)index >= (uint)nodes.Count)
                throw OutOfRange(index);

            _map._entries.Remove(nodes[index]);
            nodes.RemoveAt(index);
            if (nodes.Count == 0)
                _map._index.Remove(_key);
        }

        public bool Remove(TValue value)
        {
            int index = IndexOf(value);
            if (index < 0)
                return false;

            RemoveAt(index);
            return true;
        }

        public void Clear()
        {
            var nodes = Nodes();
            if (nodes is null)
                return;

            foreach (var node in nodes)
                _map._entries.Remove(node);

            _map._index.Remove(_key);
        }

        public int IndexOf(TValue value)
        {
            var nodes = Nodes();
            if (nodes is null)
                return -1;

            var comparer = EqualityComparer<TValue>.Default;
            for (int i = 0; i < nodes.Count; i++)
                if (comparer.Equals(nodes[i].Value.Value, value))
                    return i;

            return -1;
        }

        public bool Contains(TValue value) => IndexOf(value) >= 0;

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            var nodes = Nodes();
            if (nodes is null)
                return;

            foreach (var node in nodes)
                array[arrayIndex++] = node.Value.Value;
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            var nodes = Nodes();
            if (nodes is null)
                yield break;

            foreach (var node in nodes)
                yield return node.Value.Value;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        static ArgumentOutOfRangeException OutOfRange(int index)
            => new ArgumentOutOfRangeException(nameof(index), index, "Index out of range for the key's values.");

    }

}
