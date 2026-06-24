using System;
using System.Collections;
using System.Collections.Generic;

namespace Alembic.Util;

/// <summary>
/// An immutable list of <see cref="int"/>s with value equality: two lists with equal contents compare
/// equal and hash equally, so a list may serve as a dictionary or <see cref="Multimap{TKey, TValue}"/>
/// key.
/// </summary>
/// <remarks>
/// Subset port — only the members the planner uses are carried (the <c>copyOf</c> overloads, <c>toString</c>,
/// <c>forEach</c>, <c>append</c>/<c>range</c>/<c>identity</c>/<c>incr</c>, and the array/iterator
/// specializations are not). The empty value is a plain instance rather than Calcite's
/// <c>EmptyImmutableIntList</c> subclass, whose only purpose is to override the unported array/iterator
/// members.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.ImmutableIntList")]
public sealed class ImmutableIntList : IReadOnlyList<int>, IEquatable<ImmutableIntList>
{

    readonly int[] _ints;

    static readonly ImmutableIntList Empty = new ImmutableIntList();

    // Does not copy array. Must remain private.
    ImmutableIntList(params int[] ints)
    {
        _ints = ints;
    }

    /// <summary>
    /// Returns an empty list.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.ImmutableIntList", "of()")]
    public static ImmutableIntList Of()
    {
        return Empty;
    }

    /// <summary>
    /// Creates a list from an array of <see cref="int"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.ImmutableIntList", "of(int...)")]
    public static ImmutableIntList Of(params int[] ints)
    {
        if (ints.Length == 0)
            return Empty;

        return new ImmutableIntList((int[])ints.Clone());
    }

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.ImmutableIntList", "hashCode()")]
    public override int GetHashCode()
    {
        // Arrays.hashCode(int[]).
        int result = 1;
        foreach (var i in _ints)
            result = unchecked(31 * result + i);

        return result;
    }

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.ImmutableIntList", "equals(Object)")]
    public override bool Equals(object? obj)
    {
        // Calcite also compares against an arbitrary java.util.List (the trailing obj.equals(this) branch);
        // there is no list interop here, so only the ImmutableIntList case — Arrays.equals(ints, …) — is carried.
        return obj is ImmutableIntList other && _ints.AsSpan().SequenceEqual(other._ints);
    }

    /// <inheritdoc/>
    public bool Equals(ImmutableIntList? other)
    {
        return other is not null && _ints.AsSpan().SequenceEqual(other._ints);
    }

    /// <summary>
    /// The number of elements.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.ImmutableIntList", "size()")]
    public int Count => _ints.Length;

    /// <summary>
    /// The element at <paramref name="index"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.ImmutableIntList", "get(int)")]
    public int this[int index] => _ints[index];

    /// <summary>
    /// The element at <paramref name="index"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.ImmutableIntList", "getInt(int)")]
    public int GetInt(int index)
    {
        return _ints[index];
    }

    /// <inheritdoc/>
    public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)_ints).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _ints.GetEnumerator();

}
