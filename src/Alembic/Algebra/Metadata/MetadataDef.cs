using System;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// Describes a kind of metadata: the metadata interface and the handler interface that computes it. Each
/// metadata kind exposes one as a static <c>Def</c>, used by the provider machinery to find the handler
/// interface for a kind.
/// </summary>
/// <typeparam name="M">The metadata kind.</typeparam>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataDef")]
public class MetadataDef<M> where M : IMetadata
{
    MetadataDef(Type metadataClass, Type handlerClass)
    {
        MetadataClass = metadataClass;
        HandlerClass = handlerClass;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataDef", "metadataClass")]
    public Type MetadataClass { get; }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataDef", "handlerClass")]
    public Type HandlerClass { get; }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataDef", "of(Class, Class, Method...)")]
    public static MetadataDef<M> Of(Type metadataClass, Type handlerClass) => new MetadataDef<M>(metadataClass, handlerClass);
}
