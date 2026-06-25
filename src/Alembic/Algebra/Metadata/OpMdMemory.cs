namespace Alembic.Algebra.Metadata;

/// <summary>
/// Handler for <see cref="BuiltInMetadata.Memory"/>. An op's own memory is unknown by default; the
/// cumulative figures fold an op's memory together with its inputs' (within a phase) and divide by the
/// split count. Depends on <see cref="BuiltInMetadata.Parallelism"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory")]
public class OpMdMemory : IMetadataHandler<BuiltInMetadata.Memory>
{
    /// <summary>
    /// The provider that exposes this handler to the metadata system.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory", "SOURCE")]
    public static readonly IOpMetadataProvider Source =
        ReflectiveOpMetadataProvider.ReflectiveSource(new OpMdMemory(), typeof(BuiltInMetadata.Memory.Handler));

    /// <summary>
    /// Initializes the handler. Use <see cref="Source"/> to obtain the provider.
    /// </summary>
    protected OpMdMemory()
    {
    }

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory", "getDef()")]
    public MetadataDef<BuiltInMetadata.Memory> GetDef() => BuiltInMetadata.Memory.Def;

    /// <summary>
    /// Returns the op's own memory, which is unknown (<c>null</c>) by default.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory", "memory(RelNode, RelMetadataQuery)")]
    public double? Memory(IOp op, OpMetadataQuery mq) => null;

    /// <summary>
    /// Folds <paramref name="op"/>'s memory together with that of its inputs within the same phase (unless
    /// <paramref name="op"/> is a phase transition). Returns <c>null</c> if any figure is unknown.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory", "cumulativeMemoryWithinPhase(RelNode, RelMetadataQuery)")]
    public double? CumulativeMemoryWithinPhase(IOp op, OpMetadataQuery mq)
    {
        var nullable = mq.Memory(op);
        if (nullable is null)
            return null;

        var isPhaseTransition = mq.IsPhaseTransition(op);
        if (isPhaseTransition is null)
            return null;

        var d = nullable.Value;
        if (!isPhaseTransition.Value)
        {
            foreach (var input in op.Inputs)
            {
                nullable = mq.CumulativeMemoryWithinPhase(input);
                if (nullable is null)
                    return null;

                d += nullable.Value;
            }
        }

        return d;
    }

    /// <summary>
    /// Returns <see cref="CumulativeMemoryWithinPhase"/> divided by the op's split count, or <c>null</c> if
    /// either is unknown.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory", "cumulativeMemoryWithinPhaseSplit(RelNode, RelMetadataQuery)")]
    public double? CumulativeMemoryWithinPhaseSplit(IOp op, OpMetadataQuery mq)
    {
        var memoryWithinPhase = mq.CumulativeMemoryWithinPhase(op);
        var splitCount = mq.SplitCount(op);
        if (memoryWithinPhase is null || splitCount is null)
            return null;

        return memoryWithinPhase.Value / splitCount.Value;
    }
}
