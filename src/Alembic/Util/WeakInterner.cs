using System;
using System.Collections.Generic;

namespace Alembic.Util;

// TODO: this file should be checked against Google's Guava WeakInterner implementation, which is more
// sophisticated and has a better API. It should be made into an exact line-by-line duplicate, with .NET
// naming conventions, but any existing .NET classes used where Java or other Guava features exist.

/// <summary>
/// Interns values by equality, holding each canonical instance <em>weakly</em>: once nothing else
/// references an interned value it becomes collectible, so a long-lived interner does not pin every
/// value ever seen. Thread-safe via a single lock. Dead entries are swept periodically on insert.
/// </summary>
/// <typeparam name="T">The interned reference type.</typeparam>
[Provenance(ProvenanceSource.Other, "com.google.common.collect.Interners", "newWeakInterner()")]
public sealed class WeakInterner<T>
    where T : class
{

    const int SweepInterval = 64;

    readonly IEqualityComparer<T> _comparer;
    readonly object _gate = new object();
    readonly Dictionary<WeakKey, WeakReference<T>> _entries;
    int _sinceSweep;

    /// <summary>
    /// Creates an interner that compares values with <paramref name="comparer"/>.
    /// </summary>
    public WeakInterner(IEqualityComparer<T> comparer)
    {
        _comparer = comparer;
        _entries = new Dictionary<WeakKey, WeakReference<T>>(new WeakKeyComparer(comparer));
    }

    /// <summary>
    /// Returns the canonical instance equal to <paramref name="value"/>: the one already interned, or
    /// <paramref name="value"/> itself if it is the first of its equivalence class still alive.
    /// </summary>
    public T Intern(T value)
    {
        lock (_gate)
        {
            var key = new WeakKey(value, _comparer.GetHashCode(value));
            if (_entries.TryGetValue(key, out var existing) && existing.TryGetTarget(out var canonical))
                return canonical;

            _entries[key] = new WeakReference<T>(value);
            if (++_sinceSweep >= SweepInterval)
            {
                Sweep();
                _sinceSweep = 0;
            }

            return value;
        }
    }

    void Sweep()
    {
        List<WeakKey>? dead = null;
        foreach (var entry in _entries)
            if (!entry.Value.TryGetTarget(out _))
                (dead ??= new List<WeakKey>()).Add(entry.Key);

        if (dead is not null)
            foreach (var key in dead)
                _entries.Remove(key);
    }

    /// <summary>
    /// A dictionary key that references its value weakly and carries the value's (stable) hash so the
    /// key still hashes consistently after the value is collected.
    /// </summary>
    sealed class WeakKey
    {

        readonly WeakReference<T> _reference;

        public WeakKey(T value, int hash)
        {
            _reference = new WeakReference<T>(value);
            Hash = hash;
        }

        public int Hash { get; }

        public bool TryGetTarget(out T value) => _reference.TryGetTarget(out value!);

    }

    sealed class WeakKeyComparer : IEqualityComparer<WeakKey>
    {

        readonly IEqualityComparer<T> _comparer;

        public WeakKeyComparer(IEqualityComparer<T> comparer)
        {
            _comparer = comparer;
        }

        public int GetHashCode(WeakKey key) => key.Hash;

        public bool Equals(WeakKey? x, WeakKey? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            // A key whose value has been collected matches nothing but itself.
            if (x is null || y is null || !x.TryGetTarget(out var xValue) || !y.TryGetTarget(out var yValue))
                return false;

            return _comparer.Equals(xValue, yValue);
        }

    }

}
