using System;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// Produces a dispatcher implementing a metadata handler interface — an object whose single method per
/// metadata question routes each call to the most-specific typed handler method for the op at hand.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataHandlerProvider")]
public interface IMetadataHandlerProvider
{
    /// <summary>A dispatcher implementing <typeparamref name="THandler"/>.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataHandlerProvider", "handler(Class)")]
    THandler Handler<THandler>() where THandler : class;
}
