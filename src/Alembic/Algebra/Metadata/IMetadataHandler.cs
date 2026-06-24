namespace Alembic.Algebra.Metadata;

/// <summary>
/// Marker for a metadata handler — the object that computes a given kind of <see cref="IMetadata"/> for
/// an op. Each metadata kind declares its own handler sub-interface with the typed computation method.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataHandler")]
public interface IMetadataHandler
{
}
