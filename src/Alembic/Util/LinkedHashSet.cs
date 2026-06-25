using System;
using System.Collections;
using System.Collections.Generic;

namespace Alembic.Util;

/// <summary>
/// A set that enumerates in insertion order — the .NET stand-in for Java's <c>LinkedHashSet</c>, which is a
/// <c>HashSet</c> backed by a <c>LinkedHashMap</c> (a hash table with a doubly-linked list threaded through
/// its entries). This renders that as a <see cref="Dictionary{TKey,TValue}"/> from element to its node in a
/// <see cref="LinkedList{T}"/>: the dictionary gives O(1) membership, the linked list gives insertion-order
/// iteration and O(1) unlinking on removal. As in Java's insertion-order mode, re-adding an existing
/// element does not move it.
/// </summary>
[Provenance(ProvenanceSource.Other, "java.util.LinkedHashSet")]
internal sealed class LinkedHashSet<T> : IReadOnlySet<T>
{

    readonly Dictionary<T, LinkedListNode<T>> _map = new Dictionary<T, LinkedListNode<T>>();
    readonly LinkedList<T> _list = new LinkedList<T>();

    /// <inheritdoc/>
    public int Count => _map.Count;

    /// <summary>
    /// Adds <paramref name="item"/> at the end of the iteration order; returns <c>true</c> if it was not
    /// already a member. An already-present element keeps its position.
    /// </summary>
    public bool Add(T item)
    {
        if (_map.ContainsKey(item))
            return false;

        _map[item] = _list.AddLast(item);
        return true;
    }

    /// <summary>
    /// Removes <paramref name="item"/>; returns <c>true</c> if it was a member.
    /// </summary>
    public bool Remove(T item)
    {
        if (!_map.TryGetValue(item, out var node))
            return false;

        _list.Remove(node);
        _map.Remove(item);
        return true;
    }

    /// <summary>
    /// Removes every element matching <paramref name="match"/> in a single insertion-order pass; returns
    /// the number of elements removed.
    /// </summary>
    public int RemoveWhere(Predicate<T> match)
    {
        var removed = 0;
        for (var node = _list.First; node is not null;)
        {
            var next = node.Next;
            if (match(node.Value))
            {
                _list.Remove(node);
                _map.Remove(node.Value);
                removed++;
            }

            node = next;
        }

        return removed;
    }

    /// <inheritdoc/>
    public bool Contains(T item) => _map.ContainsKey(item);

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public bool IsProperSubsetOf(IEnumerable<T> other) => Snapshot().IsProperSubsetOf(other);

    /// <inheritdoc/>
    public bool IsProperSupersetOf(IEnumerable<T> other) => Snapshot().IsProperSupersetOf(other);

    /// <inheritdoc/>
    public bool IsSubsetOf(IEnumerable<T> other) => Snapshot().IsSubsetOf(other);

    /// <inheritdoc/>
    public bool IsSupersetOf(IEnumerable<T> other) => Snapshot().IsSupersetOf(other);

    /// <inheritdoc/>
    public bool Overlaps(IEnumerable<T> other) => Snapshot().Overlaps(other);

    /// <inheritdoc/>
    public bool SetEquals(IEnumerable<T> other) => Snapshot().SetEquals(other);

    HashSet<T> Snapshot() => new HashSet<T>(_map.Keys);

}
