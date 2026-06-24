using System;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// Thrown when a metadata request recurses back onto itself before completing (a cycle in the metadata
/// dependency graph). A shared <see cref="Instance"/> is used to avoid the cost of building a stack
/// trace on every cycle.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.CyclicMetadataException")]
public sealed class CyclicMetadataException : Exception
{
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.CyclicMetadataException", "INSTANCE")]
    public static readonly CyclicMetadataException Instance = new CyclicMetadataException();

    CyclicMetadataException()
    {
    }
}
