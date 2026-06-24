using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Alembic.Util;

/// <summary>
/// Factory methods and a builder for <see cref="IInterner{T}"/> instances — the port of Guava's
/// <c>Interners</c>. An interner is either <em>strong</em> (every interned instance is retained forever)
/// or <em>weak</em> (a canonical instance is held only weakly, so it is collectible once nothing else
/// references it). Equivalence is the element type's own <c>Equals</c>/<c>GetHashCode</c>, matching
/// Guava's <c>Equivalence.equals()</c>.
/// </summary>
[Provenance(ProvenanceSource.Other, "com.google.common.collect.Interners")]
public static class Interners
{

    /// <summary>
    /// Starts building an interner.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Interners", "newBuilder()")]
    public static InternerBuilder NewBuilder() => new InternerBuilder();

    /// <summary>
    /// Returns a new strong interner — interned instances are retained for the life of the interner.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Interners", "newStrongInterner()")]
    public static IInterner<T> NewStrongInterner<T>()
        where T : class => NewBuilder().Strong().Build<T>();

    /// <summary>
    /// Returns a new weak interner — a canonical instance is held weakly and becomes collectible once
    /// nothing else references it.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Interners", "newWeakInterner()")]
    public static IInterner<T> NewWeakInterner<T>()
        where T : class => NewBuilder().Weak().Build<T>();

    /// <summary>
    /// Builder of <see cref="IInterner{T}"/>: choose <see cref="Strong"/> or <see cref="Weak"/> retention,
    /// then <see cref="Build{T}"/>. (Guava's <c>concurrencyLevel</c> tuning knob is a no-op for the .NET
    /// backing map and is not ported.)
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Interners.InternerBuilder")]
    public sealed class InternerBuilder
    {

        bool _strong = true;

        /// <summary>
        /// Use <see cref="Interners.NewBuilder"/> to obtain a builder.
        /// </summary>
        internal InternerBuilder()
        {
        }

        /// <summary>
        /// Instructs the built interner to retain each interned instance strongly (the default).
        /// </summary>
        [Provenance(ProvenanceSource.Other, "com.google.common.collect.Interners.InternerBuilder", "strong()")]
        public InternerBuilder Strong()
        {
            _strong = true;
            return this;
        }

        /// <summary>
        /// Instructs the built interner to retain each interned instance weakly.
        /// </summary>
        [Provenance(ProvenanceSource.Other, "com.google.common.collect.Interners.InternerBuilder", "weak()")]
        public InternerBuilder Weak()
        {
            _strong = false;
            return this;
        }

        /// <summary>
        /// Builds the interner for element type <typeparamref name="T"/>.
        /// </summary>
        [Provenance(ProvenanceSource.Other, "com.google.common.collect.Interners.InternerBuilder", "build()")]
        public IInterner<T> Build<T>()
            where T : class => new InternerImpl<T>(weakKeys: !_strong);

    }

    /// <summary>
    /// The <see cref="IInterner{T}"/> implementation: delegates to an <see cref="InternMap{E}"/> whose keys
    /// are held strongly or weakly per the builder's choice.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Interners.InternerImpl")]
    sealed class InternerImpl<T> : IInterner<T>
        where T : class
    {

        readonly InternMap<T> _map;

        /// <summary>
        /// Creates an interner backed by an <see cref="InternMap{E}"/>; <paramref name="weakKeys"/> selects
        /// weak (collectible) over strong retention.
        /// </summary>
        internal InternerImpl(bool weakKeys)
        {
            _map = new InternMap<T>(weakKeys, EqualityComparer<T>.Default);
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Other, "com.google.common.collect.Interners.InternerImpl", "intern(E)")]
        public T Intern(T sample)
        {
            while (true)
            {
                // Trying to read the canonical...
                var canonical = _map.GetEntry(sample);
                if (canonical is not null)
                    return canonical;

                // Didn't see it; trying to put it instead...
                var sneaky = _map.PutIfAbsent(sample);
                if (sneaky is null)
                    return sample;

                // Someone beat us to it; trying again.
            }
        }

    }

    /// <summary>
    /// The .NET stand-in for Guava's <c>MapMakerInternalMap</c> (built via <c>createWithDummyValues</c>):
    /// a concurrent map keyed by value-equivalence whose canonical element is held weakly (when
    /// <c>weakKeys</c>) or strongly. Guava stores the canonical as the <em>key</em> with a
    /// dummy value and reads it back through <c>getEntry().getKey()</c>; .NET dictionaries don't expose a
    /// stored key, so the canonical is held in the value slot (the same <see cref="Entry"/> instance is
    /// used as both key and value) — the one place this port substitutes a .NET idiom for the Guava
    /// structure. Dead (collected) entries are swept periodically on insert.
    /// </summary>
    sealed class InternMap<E>
        where E : class
    {

        const int SweepInterval = 64;

        readonly ConcurrentDictionary<Entry, Entry> _map;
        readonly IEqualityComparer<E> _equivalence;
        readonly bool _weakKeys;
        int _sinceSweep;

        /// <summary>
        /// Creates the map; <paramref name="weakKeys"/> selects weak retention of canonical instances and
        /// <paramref name="equivalence"/> defines element equality.
        /// </summary>
        public InternMap(bool weakKeys, IEqualityComparer<E> equivalence)
        {
            _weakKeys = weakKeys;
            _equivalence = equivalence;
            _map = new ConcurrentDictionary<Entry, Entry>(new EntryComparer(equivalence));
        }

        /// <summary>The canonical instance equal to <paramref name="sample"/>, or <c>null</c> if absent.</summary>
        public E? GetEntry(E sample)
        {
            return _map.TryGetValue(LookupKey(sample), out var entry) ? entry.Get() : null;
        }

        /// <summary>
        /// Atomically: returns the live canonical instance equal to <paramref name="sample"/> if one is
        /// already present, otherwise stores <paramref name="sample"/> as the canonical and returns
        /// <c>null</c>.
        /// </summary>
        public E? PutIfAbsent(E sample)
        {
            var entry = new Entry(sample, _equivalence.GetHashCode(sample), _weakKeys);
            while (true)
            {
                if (_map.TryGetValue(entry, out var existing))
                {
                    var canonical = existing.Get();
                    if (canonical is not null)
                        return canonical;

                    // The canonical was collected; drop the dead entry and try to claim the slot.
                    ((ICollection<KeyValuePair<Entry, Entry>>)_map).Remove(new KeyValuePair<Entry, Entry>(existing, existing));
                    continue;
                }

                if (_map.TryAdd(entry, entry))
                {
                    MaybeSweep();
                    return null;
                }
            }
        }

        // A transient strong key for lookups (the sample is alive for the duration of the call), so a
        // weak interner needn't allocate a WeakReference just to probe.
        Entry LookupKey(E sample) => new Entry(sample, _equivalence.GetHashCode(sample), weak: false);

        void MaybeSweep()
        {
            if (Interlocked.Increment(ref _sinceSweep) < SweepInterval)
                return;

            Interlocked.Exchange(ref _sinceSweep, 0);
            foreach (var pair in _map)
                if (pair.Value.Get() is null)
                    ((ICollection<KeyValuePair<Entry, Entry>>)_map).Remove(pair);
        }

        /// <summary>
        /// Holds one element strongly or weakly, alongside its cached (stable) hash so the entry hashes
        /// consistently even after a weakly-held element is collected.
        /// </summary>
        sealed class Entry
        {

            readonly E? _strong;
            readonly System.WeakReference<E>? _weak;

            /// <summary>
            /// Holds <paramref name="value"/> strongly, or weakly when <paramref name="weak"/> is set, and
            /// caches <paramref name="hash"/> so the entry hashes stably after a weak value is collected.
            /// </summary>
            public Entry(E value, int hash, bool weak)
            {
                Hash = hash;
                if (weak)
                    _weak = new System.WeakReference<E>(value);
                else
                    _strong = value;
            }

            /// <summary>
            /// The element's cached hash, stable for the entry's lifetime.
            /// </summary>
            public int Hash { get; }

            /// <summary>
            /// The held element, or <c>null</c> if it was weakly held and has been collected.
            /// </summary>
            public E? Get() => _weak is null ? _strong : (_weak.TryGetTarget(out var value) ? value : null);

        }

        /// <summary>
        /// Compares <see cref="Entry"/> instances by their elements' equivalence and cached hash, treating
        /// a collected (dead) entry as equal only to itself.
        /// </summary>
        sealed class EntryComparer : IEqualityComparer<Entry>
        {

            readonly IEqualityComparer<E> _equivalence;

            /// <summary>
            /// Creates the comparer over the given element <paramref name="equivalence"/>.
            /// </summary>
            public EntryComparer(IEqualityComparer<E> equivalence)
            {
                _equivalence = equivalence;
            }

            /// <inheritdoc/>
            public int GetHashCode(Entry entry) => entry.Hash;

            /// <inheritdoc/>
            public bool Equals(Entry? x, Entry? y)
            {
                if (ReferenceEquals(x, y))
                    return true;

                // A collected element matches nothing but its own entry.
                if (x is null || y is null)
                    return false;

                var xv = x.Get();
                var yv = y.Get();
                if (xv is null || yv is null)
                    return false;

                return _equivalence.Equals(xv, yv);
            }

        }

    }

}
