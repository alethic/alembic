using System;
using System.Collections.Generic;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// A provider that chains several providers, gathering handlers from each in turn.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ChainedRelMetadataProvider")]
public class ChainedOpMetadataProvider : IOpMetadataProvider
{
    readonly IReadOnlyList<IOpMetadataProvider> _providers;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ChainedRelMetadataProvider", "ChainedRelMetadataProvider(ImmutableList)")]
    protected ChainedOpMetadataProvider(IReadOnlyList<IOpMetadataProvider> providers)
    {
        _providers = providers;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ChainedRelMetadataProvider", "of(List)")]
    public static IOpMetadataProvider Of(IReadOnlyList<IOpMetadataProvider> providers) => new ChainedOpMetadataProvider(providers);

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ChainedRelMetadataProvider", "handlers(Class)")]
    public IReadOnlyList<IMetadataHandler> Handlers(Type handlerClass)
    {
        var handlers = new List<IMetadataHandler>();
        foreach (var provider in _providers)
            handlers.AddRange(provider.Handlers(handlerClass));

        return handlers;
    }
}
