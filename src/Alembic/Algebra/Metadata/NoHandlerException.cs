using System;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// Thrown when no handler is registered for an op's type for a given metadata kind.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataHandlerProvider.NoHandler")]
public sealed class NoHandlerException : Exception
{
    public NoHandlerException(Type opType)
        : base($"No metadata handler registered for op type {opType}")
    {
        OpType = opType;
    }

    public Type OpType { get; }
}
