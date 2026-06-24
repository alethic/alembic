namespace Alembic.Algebra.Metadata;

/// <summary>
/// Handler for <see cref="BuiltInMetadata.Memory"/>. An op's own memory is unknown by default; the
/// cumulative figures fold an op's memory together with its inputs' (within a phase) and divide by the
/// split count. Depends on <see cref="BuiltInMetadata.Parallelism"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory")]
public sealed class OpMdMemory : BuiltInMetadata.Memory.Handler
{
    delegate double? Impl(IOp op, OpMetadataQuery mq);

    readonly MetadataRegistry<Impl> _memory = new MetadataRegistry<Impl>();
    readonly MetadataRegistry<Impl> _cumulativeMemoryWithinPhase = new MetadataRegistry<Impl>();
    readonly MetadataRegistry<Impl> _cumulativeMemoryWithinPhaseSplit = new MetadataRegistry<Impl>();

    public OpMdMemory()
    {
        _memory.RegisterDefault((op, mq) => null);

        _cumulativeMemoryWithinPhase.RegisterDefault((op, mq) =>
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
                foreach (var input in op.Children)
                {
                    nullable = mq.CumulativeMemoryWithinPhase(input);
                    if (nullable is null)
                        return null;

                    d += nullable.Value;
                }
            }

            return d;
        });

        _cumulativeMemoryWithinPhaseSplit.RegisterDefault((op, mq) =>
        {
            var memoryWithinPhase = mq.CumulativeMemoryWithinPhase(op);
            var splitCount = mq.SplitCount(op);
            if (memoryWithinPhase is null || splitCount is null)
                return null;

            return memoryWithinPhase.Value / splitCount.Value;
        });
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory", "memory(RelNode, RelMetadataQuery)")]
    public double? Memory(IOp op, OpMetadataQuery mq) => _memory.Resolve(op)(op, mq);

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory", "cumulativeMemoryWithinPhase(RelNode, RelMetadataQuery)")]
    public double? CumulativeMemoryWithinPhase(IOp op, OpMetadataQuery mq) => _cumulativeMemoryWithinPhase.Resolve(op)(op, mq);

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory", "cumulativeMemoryWithinPhaseSplit(RelNode, RelMetadataQuery)")]
    public double? CumulativeMemoryWithinPhaseSplit(IOp op, OpMetadataQuery mq) => _cumulativeMemoryWithinPhaseSplit.Resolve(op)(op, mq);
}
