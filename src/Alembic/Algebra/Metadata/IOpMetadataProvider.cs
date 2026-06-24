using System;
using System.Collections.Generic;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// A source of metadata handlers. Given a handler interface, returns the handler objects that supply it.
/// Providers are chained (see <see cref="ChainedOpMetadataProvider"/>) into one central provider.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataProvider")]
public interface IOpMetadataProvider
{
    /// <summary>The handlers this provider supplies for the given handler interface.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataProvider", "handlers(Class)")]
    IReadOnlyList<IMetadataHandler> Handlers(Type handlerClass);
}
