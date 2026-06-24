namespace Alembic.Algebra.Metadata;

/// <summary>
/// Marker for a kind of metadata — a derived property that can be asked of an op (its cost, and so on).
/// Each kind also declares a nested <c>Handler</c> that computes it.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.Metadata")]
public interface IMetadata
{
}
