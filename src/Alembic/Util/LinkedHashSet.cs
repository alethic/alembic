using System;
using System.Collections;
using System.Collections.Generic;

namespace Alembic.Util;

/// <summary>
/// A set that enumerates in insertion order — the .NET stand-in for Java's <c>LinkedHashSet</c> (the BCL
/// <see cref="HashSet{T}"/> does not guarantee iteration order). Membership and the set predicates are
/// delegated to an inner <see cref="HashSet{T}"/>; a parallel list preserves order.
/// </summary>
internal sealed class LinkedHashSet<T> : IReadOnlySet<T>
{

    readonly HashSet<T> _set = new HashSet<T>();
    readonly List<T> _order = new List<T>();

    public int Count => _set.Count;

    public bool Add(T item)
    {
        if (!_set.Add(item))
            return false;

        _order.Add(item);
        return true;
    }

    public bool Remove(T item)
    {
        if (!_set.Remove(item))
            return false;

        _order.Remove(item);
        return true;
    }

    public int RemoveWhere(Predicate<T> match)
    {
        int removed = _order.RemoveAll(match);
        if (removed > 0)
            _set.RemoveWhere(match);

        return removed;
    }

    public bool Contains(T item) => _set.Contains(item);

    public IEnumerator<T> GetEnumerator() => _order.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool IsProperSubsetOf(IEnumerable<T> other) => _set.IsProperSubsetOf(other);

    public bool IsProperSupersetOf(IEnumerable<T> other) => _set.IsProperSupersetOf(other);

    public bool IsSubsetOf(IEnumerable<T> other) => _set.IsSubsetOf(other);

    public bool IsSupersetOf(IEnumerable<T> other) => _set.IsSupersetOf(other);

    public bool Overlaps(IEnumerable<T> other) => _set.Overlaps(other);

    public bool SetEquals(IEnumerable<T> other) => _set.SetEquals(other);

}
