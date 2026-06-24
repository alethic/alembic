using System;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// Thrown when no handler is registered for an op's type for a given metadata kind.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.MetadataHandlerProvider.NoHandler")]
public sealed class NoHandlerException : Exception
{
    /// <summary>
    /// Creates the exception for the op type <paramref name="opType"/> that has no registered handler.
    /// </summary>
    public NoHandlerException(Type opType)
        : base($"No metadata handler registered for op type {opType}")
    {
        OpType = opType;
    }

    /// <summary>
    /// The op type for which no handler was registered.
    /// </summary>
    public Type OpType { get; }
}
