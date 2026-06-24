namespace Alembic.Algebra.Metadata;

/// <summary>
/// Handler for <see cref="BuiltInMetadata.Parallelism"/>. The generic defaults are that an op is not a
/// phase transition and has a single split. (Calcite's per-op overrides — <c>TableScan</c>, <c>Values</c>,
/// <c>Exchange</c> — are relational and not ported.)
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdParallelism")]
public sealed class OpMdParallelism : BuiltInMetadata.Parallelism.Handler
{
    delegate bool? PhaseTransitionImpl(IOp op, OpMetadataQuery mq);

    delegate int? SplitCountImpl(IOp op, OpMetadataQuery mq);

    readonly MetadataRegistry<PhaseTransitionImpl> _phaseTransition = new MetadataRegistry<PhaseTransitionImpl>();
    readonly MetadataRegistry<SplitCountImpl> _splitCount = new MetadataRegistry<SplitCountImpl>();

    public OpMdParallelism()
    {
        _phaseTransition.RegisterDefault((op, mq) => false);
        _splitCount.RegisterDefault((op, mq) => 1);
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdParallelism", "isPhaseTransition(RelNode, RelMetadataQuery)")]
    public bool? IsPhaseTransition(IOp op, OpMetadataQuery mq) => _phaseTransition.Resolve(op)(op, mq);

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdParallelism", "splitCount(RelNode, RelMetadataQuery)")]
    public int? SplitCount(IOp op, OpMetadataQuery mq) => _splitCount.Resolve(op)(op, mq);
}
