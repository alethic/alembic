namespace Alembic.Algebra.Metadata;

/// <summary>
/// Non-generic base of <see cref="IMetadataHandler{M}"/>, the analog of Calcite's <c>MetadataHandler&lt;?&gt;</c>
/// wildcard: lets the provider machinery hold handlers of any metadata kind in one collection.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataHandler")]
public interface IMetadataHandler
{

}

/// <summary>
/// A metadata handler — a class that supplies typed methods computing a given kind of
/// <see cref="IMetadata"/> for specific op types. The reflective provider discovers those typed methods
/// and routes each request to the most-specific one for the op at hand.
/// </summary>
/// <typeparam name="M">The metadata kind this handler computes.</typeparam>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataHandler")]
public interface IMetadataHandler<M> : IMetadataHandler where M : IMetadata
{

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataHandler", "getDef()")]
    MetadataDef<M> GetDef();

}
