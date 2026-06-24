namespace Alembic.Algebra.Metadata;

/// <summary>
/// Implemented by an op that is a thin wrapper around another op (such as a HEP graph vertex) and wants
/// metadata requests to be answered by that underlying op. The metadata query unwraps such an op to its
/// delegate before computing or caching any metadata.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.DelegatingMetadataRel")]
public interface IDelegatingMetadataOp
{
    /// <summary>
    /// Returns the underlying op that metadata requests should be delegated to.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.DelegatingMetadataRel", "getMetadataDelegateRel()")]
    IOp GetMetadataDelegateRel();
}
