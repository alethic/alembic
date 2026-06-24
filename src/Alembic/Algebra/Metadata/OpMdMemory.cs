namespace Alembic.Algebra.Metadata;

/// <summary>
/// Handler for <see cref="BuiltInMetadata.Memory"/>. An op's own memory is unknown by default; the
/// cumulative figures fold an op's memory together with its inputs' (within a phase) and divide by the
/// split count. Depends on <see cref="BuiltInMetadata.Parallelism"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory")]
public class OpMdMemory : IMetadataHandler<BuiltInMetadata.Memory>
{
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory", "SOURCE")]
    public static readonly IOpMetadataProvider Source =
        ReflectiveOpMetadataProvider.ReflectiveSource(new OpMdMemory(), typeof(BuiltInMetadata.Memory.Handler));

    protected OpMdMemory()
    {
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory", "getDef()")]
    public MetadataDef<BuiltInMetadata.Memory> GetDef() => BuiltInMetadata.Memory.Def;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdMemory", "memory(RelNode, RelMetadataQuery)")]
    public double? Memory(IOp op, OpMetadataQuery mq) => null;

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
            foreach (var input in op.Children)
            {
                nullable = mq.CumulativeMemoryWithinPhase(input);
                if (nullable is null)
                    return null;

                d += nullable.Value;
            }
        }

        return d;
    }

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
