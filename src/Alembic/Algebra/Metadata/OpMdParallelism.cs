namespace Alembic.Algebra.Metadata;

/// <summary>
/// Handler for <see cref="BuiltInMetadata.Parallelism"/>. The generic defaults are that an op is not a
/// phase transition and has a single split. (Calcite's per-op overrides — <c>TableScan</c>, <c>Values</c>,
/// <c>Exchange</c> — are relational and not ported.)
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdParallelism")]
public class OpMdParallelism : IMetadataHandler<BuiltInMetadata.Parallelism>
{
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdParallelism", "SOURCE")]
    public static readonly IOpMetadataProvider Source =
        ReflectiveOpMetadataProvider.ReflectiveSource(new OpMdParallelism(), typeof(BuiltInMetadata.Parallelism.Handler));

    protected OpMdParallelism()
    {
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdParallelism", "getDef()")]
    public MetadataDef<BuiltInMetadata.Parallelism> GetDef() => BuiltInMetadata.Parallelism.Def;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdParallelism", "isPhaseTransition(RelNode, RelMetadataQuery)")]
    public bool? IsPhaseTransition(IOp op, OpMetadataQuery mq) => false;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdParallelism", "splitCount(RelNode, RelMetadataQuery)")]
    public int? SplitCount(IOp op, OpMetadataQuery mq) => 1;
}
