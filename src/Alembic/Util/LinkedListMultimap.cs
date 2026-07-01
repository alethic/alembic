using System;
using System.Collections;
using System.Collections.Generic;

namespace Alembic.Util;

/// <summary>
/// A multimap that keeps every key→value entry in a single doubly-linked chain (preserving overall
/// insertion order) with a per-key doubly-linked sibling chain (preserving per-key insertion order). A
/// given key→value pair may be stored more than once. A faithful port of Guava's
/// <c>LinkedListMultimap</c>: each <see cref="Node"/> is linked both globally (<see cref="_head"/>/
/// <see cref="_tail"/> + <see cref="Node.Next"/>/<see cref="Node.Previous"/>) and per key
/// (<see cref="Node.NextSibling"/>/<see cref="Node.PreviousSibling"/>, indexed by <see cref="_keyToKeyList"/>),
/// so an entry is inserted or removed in O(1) given its node.
/// </summary>
/// <remarks>
/// As in Guava, <see cref="Get"/> hands back a live, mutate-through view over a key's values (not a
/// snapshot): reads reflect the current state and add/insert/remove/set change the multimap in place.
/// <c>values().removeIf(...)</c> is exposed narrowly as <see cref="RemoveValuesWhere"/>. Guava's
/// <c>size</c>/<c>modCount</c> are not ported: there is no <c>size()</c> consumer, and callers copy the
/// live view before iterating, so no fail-fast is needed.
/// </remarks>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
[Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap")]
public sealed class LinkedListMultimap<TKey, TValue>
    where TKey : notnull
{

    // The head and tail of the global chain of all entries, in insertion order.
    Node? _head;
    Node? _tail;

    // Per-key sibling chains, indexed by key (= Guava's keyToKeyList).
    readonly Dictionary<TKey, KeyList> _keyToKeyList = new();

    /// <summary>
    /// One key→value entry, linked into both the global chain (<see cref="Next"/>/<see cref="Previous"/>)
    /// and its key's sibling chain (<see cref="NextSibling"/>/<see cref="PreviousSibling"/>).
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap.Node")]
    sealed class Node
    {

        internal readonly TKey Key;
        internal TValue Value;
        internal Node? Next;
        internal Node? Previous;
        internal Node? NextSibling;
        internal Node? PreviousSibling;

        internal Node(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }

    }

    /// <summary>
    /// The head, tail, and count of one key's sibling chain (= Guava's <c>KeyList</c>). A key present in
    /// <see cref="_keyToKeyList"/> always has at least one node, so <see cref="Head"/>/<see cref="Tail"/>
    /// are non-null.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap.KeyList")]
    sealed class KeyList
    {

        internal Node Head;
        internal Node Tail;
        internal int Count;

        [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap.KeyList", "KeyList(Node)")]
        internal KeyList(Node firstNode)
        {
            Head = firstNode;
            Tail = firstNode;
            firstNode.PreviousSibling = null;
            firstNode.NextSibling = null;
            Count = 1;
        }

    }

    /// <summary>
    /// Adds a node for <paramref name="key"/>→<paramref name="value"/> before <paramref name="nextSibling"/>
    /// in the key's chain (and correspondingly in the global chain), or at the end of both when
    /// <paramref name="nextSibling"/> is <c>null</c>. O(1).
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap", "addNode(K, V, Node)")]
    Node AddNode(TKey key, TValue value, Node? nextSibling)
    {
        var node = new Node(key, value);
        if (_head is null)
        {
            // Empty list.
            _head = _tail = node;
            _keyToKeyList[key] = new KeyList(node);
        }
        else if (nextSibling is null)
        {
            // Non-empty list, add to tail.
            _tail!.Next = node;
            node.Previous = _tail;
            _tail = node;
            if (!_keyToKeyList.TryGetValue(key, out var keyList))
            {
                _keyToKeyList[key] = new KeyList(node);
            }
            else
            {
                keyList.Count++;
                var keyTail = keyList.Tail;
                keyTail.NextSibling = node;
                node.PreviousSibling = keyTail;
                keyList.Tail = node;
            }
        }
        else
        {
            // Non-empty list, insert before nextSibling.
            var keyList = _keyToKeyList[key];
            keyList.Count++;
            node.Previous = nextSibling.Previous;
            node.PreviousSibling = nextSibling.PreviousSibling;
            node.Next = nextSibling;
            node.NextSibling = nextSibling;
            if (nextSibling.PreviousSibling is null)
                keyList.Head = node; // nextSibling was the key head
            else
                nextSibling.PreviousSibling.NextSibling = node;

            if (nextSibling.Previous is null)
                _head = node; // nextSibling was the global head
            else
                nextSibling.Previous.Next = node;

            nextSibling.Previous = node;
            nextSibling.PreviousSibling = node;
        }

        return node;
    }

    /// <summary>
    /// Unlinks <paramref name="node"/> from the global chain and its key's chain, dropping the key when it
    /// held the last value. O(1).
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap", "removeNode(Node)")]
    void RemoveNode(Node node)
    {
        if (node.Previous is not null)
            node.Previous.Next = node.Next;
        else
            _head = node.Next; // node was head

        if (node.Next is not null)
            node.Next.Previous = node.Previous;
        else
            _tail = node.Previous; // node was tail

        if (node.PreviousSibling is null && node.NextSibling is null)
        {
            _keyToKeyList.Remove(node.Key);
        }
        else
        {
            var keyList = _keyToKeyList[node.Key];
            keyList.Count--;
            if (node.PreviousSibling is null)
                keyList.Head = node.NextSibling!;
            else
                node.PreviousSibling.NextSibling = node.NextSibling;

            if (node.NextSibling is null)
                keyList.Tail = node.PreviousSibling!;
            else
                node.NextSibling.PreviousSibling = node.PreviousSibling;
        }
    }

    /// <summary>
    /// Stores <paramref name="value"/> with <paramref name="key"/>, appending a node to both the overall
    /// chain and the key's chain (= Guava's <c>addNode(key, value, null)</c>). Always returns <c>true</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap", "put(K, V)")]
    public bool Put(TKey key, TValue value)
    {
        AddNode(key, value, null);
        return true;
    }

    /// <summary>
    /// A live, mutate-through view of the values associated with <paramref name="key"/>, in insertion
    /// order — Guava's <c>get(K)</c> (an <c>AbstractSequentialList</c> backed by the key's sibling chain).
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap", "get(K)")]
    public IList<TValue> Get(TKey key)
    {
        return new ValuesForKey(this, key);
    }

    /// <summary>
    /// Removes every value (across all keys) matching <paramref name="predicate"/>, walking the overall
    /// chain in insertion order — the .NET stand-in for <c>values().removeIf(predicate)</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap", "values()")]
    public void RemoveValuesWhere(Func<TValue, bool> predicate)
    {
        for (var node = _head; node is not null;)
        {
            var next = node.Next;
            if (predicate(node.Value))
                RemoveNode(node);

            node = next;
        }
    }

    /// <summary>
    /// Removes every key→value association.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.LinkedListMultimap", "clear()")]
    public void Clear()
    {
        _head = null;
        _tail = null;
        _keyToKeyList.Clear();
    }

    /// <summary>
    /// The live view returned by <see cref="Get"/> — the .NET equivalent of Guava's <c>get(K)</c> view and
    /// its <c>ValueForKeyIterator</c>. It holds only the key, resolving the key's sibling chain on each
    /// access, so it reflects concurrent additions/removals; mutations go through the O(1)
    /// <see cref="AddNode"/>/<see cref="RemoveNode"/>.
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

        KeyList? ResolveKeyList()
        {
            return _map._keyToKeyList.GetValueOrDefault(_key);
        }

        public int Count
        {
            get { return ResolveKeyList()?.Count ?? 0; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        // The node at the given per-key position, walking from whichever end is closer (as Guava's
        // ValueForKeyIterator constructor does).
        Node NodeAt(int index)
        {
            var keyList = ResolveKeyList();
            int count = keyList?.Count ?? 0;
            if ((uint)index >= (uint)count)
                throw OutOfRange(index);

            Node node;
            if (index < count - index)
            {
                node = keyList!.Head;
                for (int i = 0; i < index; i++)
                    node = node.NextSibling!;
            }
            else
            {
                node = keyList!.Tail;
                for (int i = count - 1; i > index; i--)
                    node = node.PreviousSibling!;
            }

            return node;
        }

        public TValue this[int index]
        {
            get { return NodeAt(index).Value; }
            set { NodeAt(index).Value = value; }
        }

        public void Add(TValue value)
        {
            _map.AddNode(_key, value, null);
        }

        public void Insert(int index, TValue value)
        {
            int count = Count;
            if ((uint)index > (uint)count)
                throw OutOfRange(index);

            // Splice before the node currently at `index`, or append when inserting at the end.
            var nextSibling = index == count ? null : NodeAt(index);
            _map.AddNode(_key, value, nextSibling);
        }

        public void RemoveAt(int index)
        {
            _map.RemoveNode(NodeAt(index));
        }

        public bool Remove(TValue value)
        {
            var comparer = EqualityComparer<TValue>.Default;
            for (var node = ResolveKeyList()?.Head; node is not null; node = node.NextSibling)
            {
                if (comparer.Equals(node.Value, value))
                {
                    _map.RemoveNode(node);
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            for (var node = ResolveKeyList()?.Head; node is not null;)
            {
                var next = node.NextSibling;
                _map.RemoveNode(node);
                node = next;
            }
        }

        public int IndexOf(TValue value)
        {
            var comparer = EqualityComparer<TValue>.Default;
            int index = 0;
            for (var node = ResolveKeyList()?.Head; node is not null; node = node.NextSibling, index++)
                if (comparer.Equals(node.Value, value))
                    return index;

            return -1;
        }

        public bool Contains(TValue value)
        {
            return IndexOf(value) >= 0;
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            for (var node = ResolveKeyList()?.Head; node is not null; node = node.NextSibling)
                array[arrayIndex++] = node.Value;
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            for (var node = ResolveKeyList()?.Head; node is not null; node = node.NextSibling)
                yield return node.Value;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        static ArgumentOutOfRangeException OutOfRange(int index)
        {
            return new ArgumentOutOfRangeException(nameof(index), index, "Index out of range for the key's values.");
        }

    }

}
