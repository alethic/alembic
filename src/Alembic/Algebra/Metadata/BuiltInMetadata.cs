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
    /// The lower bound of the cost of an op, used by the cost-based planner to prune a group whose best
    /// possible cost already exceeds the incumbent.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.LowerBoundCost")]
    public interface LowerBoundCost : IMetadata
    {
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
