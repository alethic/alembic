namespace Alembic.Algebra.Metadata;

/// <summary>
/// A metadata handler — a class that supplies typed methods computing a given kind of
/// <see cref="IMetadata"/> for specific op types. The reflective provider discovers those typed methods
/// and routes each request to the most-specific one for the op at hand.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataHandler")]
public interface IMetadataHandler
{
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataHandler", "getDef()")]
    MetadataDef GetDef();
}
