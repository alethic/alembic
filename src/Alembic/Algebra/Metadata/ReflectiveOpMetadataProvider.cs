using System;
using System.Collections.Generic;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// A provider built from a single handler object. Reflection over the handler's typed methods is done by
/// <see cref="ProxyingMetadataHandlerProvider"/> when it builds a dispatcher; this provider's job is to
/// hold the handler and answer which handler interface it supplies.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ReflectiveRelMetadataProvider")]
public sealed class ReflectiveOpMetadataProvider : IOpMetadataProvider
{
    readonly Type _handlerClass;
    readonly IReadOnlyList<IMetadataHandler> _handlers;

    ReflectiveOpMetadataProvider(IMetadataHandler handler, Type handlerClass)
    {
        _handlerClass = handlerClass;
        _handlers = new[] { handler };
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ReflectiveRelMetadataProvider", "reflectiveSource(MetadataHandler, Class)")]
    public static IOpMetadataProvider ReflectiveSource(IMetadataHandler handler, Type handlerClass)
        => new ReflectiveOpMetadataProvider(handler, handlerClass);

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ReflectiveRelMetadataProvider", "handlers(Class)")]
    public IReadOnlyList<IMetadataHandler> Handlers(Type handlerClass)
        => _handlerClass.IsAssignableFrom(handlerClass) ? _handlers : Array.Empty<IMetadataHandler>();
}
