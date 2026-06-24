using Alembic.Plan;
using Alembic.Plan.Volcano;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// Contains the kinds of metadata Alembic computes. Calcite's <c>BuiltInMetadata</c> declares some
/// thirty kinds; Alembic keeps only the cost kinds, since the rest (row counts, selectivity, unique
/// keys, column origins, …) are relational.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata")]
public static class BuiltInMetadata
{
    /// <summary>
    /// Metadata about the cost of evaluating an op, <em>including</em> the cost of its inputs.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.CumulativeCost")]
    public interface CumulativeCost : IMetadata
    {
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.CumulativeCost", "DEF")]
        public static readonly MetadataDef Def = MetadataDef.Of(typeof(CumulativeCost), typeof(Handler));

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.CumulativeCost", "getCumulativeCost()")]
        IOpCost? GetCumulativeCost();

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.CumulativeCost.Handler")]
        public interface Handler : IMetadataHandler
        {
            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.CumulativeCost.Handler", "getCumulativeCost(RelNode, RelMetadataQuery)")]
            IOpCost? GetCumulativeCost(IOp op, OpMetadataQuery mq);
        }
    }

    /// <summary>
    /// Metadata about the cost of evaluating an op, <em>not</em> including the cost of its inputs.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.NonCumulativeCost")]
    public interface NonCumulativeCost : IMetadata
    {
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.NonCumulativeCost", "DEF")]
        public static readonly MetadataDef Def = MetadataDef.Of(typeof(NonCumulativeCost), typeof(Handler));

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.NonCumulativeCost", "getNonCumulativeCost()")]
        IOpCost? GetNonCumulativeCost();

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.NonCumulativeCost.Handler")]
        public interface Handler : IMetadataHandler
        {
            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.NonCumulativeCost.Handler", "getNonCumulativeCost(RelNode, RelMetadataQuery)")]
            IOpCost? GetNonCumulativeCost(IOp op, OpMetadataQuery mq);
        }
    }

    /// <summary>
    /// Metadata about the memory (in bytes) an op uses. Not relational — a physical-resource property.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory")]
    public interface Memory : IMetadata
    {
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory", "DEF")]
        public static readonly MetadataDef Def = MetadataDef.Of(typeof(Memory), typeof(Handler));

        // Calcite also declares the accessor `memory()`; C# forbids a member named the same as its
        // enclosing type, so it is omitted here. (These accessors are the old metadata-proxy API and are
        // unused by the explicit-registry dispatch; the Handler below carries the real port.)
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory", "cumulativeMemoryWithinPhase()")]
        double? CumulativeMemoryWithinPhase();

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory", "cumulativeMemoryWithinPhaseSplit()")]
        double? CumulativeMemoryWithinPhaseSplit();

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory.Handler")]
        public interface Handler : IMetadataHandler
        {
            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory.Handler", "memory(RelNode, RelMetadataQuery)")]
            double? Memory(IOp op, OpMetadataQuery mq);

            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory.Handler", "cumulativeMemoryWithinPhase(RelNode, RelMetadataQuery)")]
            double? CumulativeMemoryWithinPhase(IOp op, OpMetadataQuery mq);

            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory.Handler", "cumulativeMemoryWithinPhaseSplit(RelNode, RelMetadataQuery)")]
            double? CumulativeMemoryWithinPhaseSplit(IOp op, OpMetadataQuery mq);
        }
    }

    /// <summary>
    /// Metadata about the parallel execution of an op. Not relational — a physical-execution property.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism")]
    public interface Parallelism : IMetadata
    {
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism", "DEF")]
        public static readonly MetadataDef Def = MetadataDef.Of(typeof(Parallelism), typeof(Handler));

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism", "isPhaseTransition()")]
        bool? IsPhaseTransition();

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism", "splitCount()")]
        int? SplitCount();

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism.Handler")]
        public interface Handler : IMetadataHandler
        {
            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism.Handler", "isPhaseTransition(RelNode, RelMetadataQuery)")]
            bool? IsPhaseTransition(IOp op, OpMetadataQuery mq);

            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism.Handler", "splitCount(RelNode, RelMetadataQuery)")]
            int? SplitCount(IOp op, OpMetadataQuery mq);
        }
    }

    /// <summary>
    /// The lower bound of the cost of an op, used by the cost-based planner to prune a group whose best
    /// possible cost already exceeds the incumbent.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.LowerBoundCost")]
    public interface LowerBoundCost : IMetadata
    {
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.LowerBoundCost", "DEF")]
        public static readonly MetadataDef Def = MetadataDef.Of(typeof(LowerBoundCost), typeof(Handler));

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.LowerBoundCost", "getLowerBoundCost(VolcanoPlanner)")]
        IOpCost? GetLowerBoundCost(VolcanoPlanner planner);

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.LowerBoundCost.Handler")]
        public interface Handler : IMetadataHandler
        {
            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.LowerBoundCost.Handler", "getLowerBoundCost(RelNode, RelMetadataQuery, VolcanoPlanner)")]
            IOpCost? GetLowerBoundCost(IOp op, OpMetadataQuery mq, VolcanoPlanner planner);
        }
    }
}
