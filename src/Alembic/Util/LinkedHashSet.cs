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

    /// <inheritdoc/>
    public int Count => _set.Count;

    /// <summary>
    /// Adds <paramref name="item"/> at the end of the iteration order; returns <c>true</c> if it was not
    /// already a member.
    /// </summary>
    public bool Add(T item)
    {
        if (!_set.Add(item))
            return false;

        _order.Add(item);
        return true;
    }

    /// <summary>
    /// Removes <paramref name="item"/>; returns <c>true</c> if it was a member.
    /// </summary>
    public bool Remove(T item)
    {
        if (!_set.Remove(item))
            return false;

        _order.Remove(item);
        return true;
    }

    /// <summary>
    /// Removes every element matching <paramref name="match"/>; returns the number of elements removed.
    /// </summary>
    public int RemoveWhere(Predicate<T> match)
    {
        int removed = _order.RemoveAll(match);
        if (removed > 0)
            _set.RemoveWhere(match);

        return removed;
    }

    /// <inheritdoc/>
    public bool Contains(T item) => _set.Contains(item);

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _order.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public bool IsProperSubsetOf(IEnumerable<T> other) => _set.IsProperSubsetOf(other);

    /// <inheritdoc/>
    public bool IsProperSupersetOf(IEnumerable<T> other) => _set.IsProperSupersetOf(other);

    /// <inheritdoc/>
    public bool IsSubsetOf(IEnumerable<T> other) => _set.IsSubsetOf(other);

    /// <inheritdoc/>
    public bool IsSupersetOf(IEnumerable<T> other) => _set.IsSupersetOf(other);

    /// <inheritdoc/>
    public bool Overlaps(IEnumerable<T> other) => _set.Overlaps(other);

    /// <inheritdoc/>
    public bool SetEquals(IEnumerable<T> other) => _set.SetEquals(other);

}
