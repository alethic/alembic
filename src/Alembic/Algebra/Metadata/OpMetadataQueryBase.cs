using System;
using System.Collections.Generic;

using Alembic.Plan;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// Base of <see cref="OpMetadataQuery"/>: the per-query result cache and the cycle handling around it.
/// Each kind's query method runs its computation through <see cref="Cache"/>, so a result is computed at
/// most once per op per query, and a request that recurses onto itself is detected.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQueryBase")]
public abstract class OpMetadataQueryBase
{
    /// <summary>Set of active metadata queries, and cache of previous results, keyed by op then kind.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQueryBase", "map")]
    protected readonly Dictionary<IOp, Dictionary<object, object?>> Map = new Dictionary<IOp, Dictionary<object, object?>>();

    /// <summary>
    /// Returns the cached value for <paramref name="op"/> and <paramref name="key"/>, or computes it via
    /// <paramref name="compute"/> and caches it. While a value is being computed it is marked active, so
    /// a recursive request for the same value throws <see cref="CyclicMetadataException"/>.
    /// </summary>
    protected T Cache<T>(IOp op, object key, Func<T> compute)
    {
        if (!Map.TryGetValue(op, out var row))
            Map[op] = row = new Dictionary<object, object?>();

        if (row.TryGetValue(key, out var cached))
        {
            if (ReferenceEquals(cached, NullSentinel.Active))
                throw CyclicMetadataException.Instance;

            return ReferenceEquals(cached, NullSentinel.Instance) ? default! : (T)cached!;
        }

        row[key] = NullSentinel.Active;
        try
        {
            var result = compute();
            row[key] = NullSentinel.Mask(result);
            return result;
        }
        catch (Exception)
        {
            // Clear the op's row so a later request can retry from a clean state.
            Map.Remove(op);
            throw;
        }
    }

    /// <summary>
    /// Unwraps an op that delegates its metadata (e.g. a HEP vertex) to the op that actually answers for
    /// it, so metadata is computed and cached against the real op.
    /// </summary>
    protected static IOp Delegate(IOp op)
    {
        while (op is IDelegatingMetadataOp delegating)
            op = delegating.GetMetadataDelegateRel();

        return op;
    }

    /// <summary>Removes cached metadata for <paramref name="op"/>. Returns whether its row was non-empty.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQueryBase", "clearCache(RelNode)")]
    public bool ClearCache(IOp op)
    {
        if (!Map.TryGetValue(op, out var row) || row.Count == 0)
            return false;

        row.Clear();
        return true;
    }
}
