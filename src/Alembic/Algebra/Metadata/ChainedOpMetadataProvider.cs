using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// A provider that chains several providers, gathering handlers from each in turn.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ChainedRelMetadataProvider")]
public class ChainedOpMetadataProvider : IOpMetadataProvider
{
    readonly IReadOnlyList<IOpMetadataProvider> _providers;

    /// <summary>
    /// Creates a provider that chains <paramref name="providers"/> in order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ChainedRelMetadataProvider", "ChainedRelMetadataProvider(ImmutableList)")]
    protected ChainedOpMetadataProvider(IReadOnlyList<IOpMetadataProvider> providers)
    {
        _providers = providers;
        Debug.Assert(!_providers.Contains(this));
    }

    /// <summary>
    /// Creates a provider that chains <paramref name="providers"/> in order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ChainedRelMetadataProvider", "of(List)")]
    public static IOpMetadataProvider Of(IReadOnlyList<IOpMetadataProvider> providers) => new ChainedOpMetadataProvider(providers);

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ChainedRelMetadataProvider", "handlers(Class)")]
    public IReadOnlyList<IMetadataHandler> Handlers(Type handlerClass)
    {
        var handlers = new List<IMetadataHandler>();
        foreach (var provider in _providers)
            handlers.AddRange(provider.Handlers(handlerClass));

        return handlers;
    }

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ChainedRelMetadataProvider", "equals(Object)")]
    public override bool Equals(object? obj)
        => obj is ChainedOpMetadataProvider other && _providers.SequenceEqual(other._providers);

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ChainedRelMetadataProvider", "hashCode()")]
    public override int GetHashCode()
    {
        var hash = 1;
        foreach (var provider in _providers)
            hash = 31 * hash + (provider?.GetHashCode() ?? 0);

        return hash;
    }
}
