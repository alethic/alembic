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

        /// <summary>
        /// The definition of this metadata kind.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.CumulativeCost", "DEF")]
        public static readonly MetadataDef<CumulativeCost> Def = MetadataDef<CumulativeCost>.Of(typeof(CumulativeCost), typeof(Handler));

        /// <summary>
        /// Estimates the cost of executing an op, including the cost of its inputs. The default
        /// implementation adds <see cref="NonCumulativeCost.GetNonCumulativeCost"/> to the cumulative cost
        /// of each input, but a metadata provider can override this with its own cost model, e.g. to take
        /// into account interactions between ops.
        /// </summary>
        /// <returns>The estimated cost, or <c>null</c> if no reliable estimate can be determined.</returns>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.CumulativeCost", "getCumulativeCost()")]
        IOpCost? GetCumulativeCost();

        /// <summary>
        /// The handler API for <see cref="CumulativeCost"/> metadata.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.CumulativeCost.Handler")]
        public interface Handler : IMetadataHandler<CumulativeCost>
        {
            /// <summary>
            /// Estimates the cumulative cost of <paramref name="op"/>.
            /// </summary>
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

        /// <summary>
        /// The definition of this metadata kind.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.NonCumulativeCost", "DEF")]
        public static readonly MetadataDef<NonCumulativeCost> Def = MetadataDef<NonCumulativeCost>.Of(typeof(NonCumulativeCost), typeof(Handler));

        /// <summary>
        /// Estimates the cost of executing an op, not counting the cost of its inputs. (However, the
        /// non-cumulative cost is still usually dependent on the row counts of the inputs.) The default
        /// implementation asks the op itself via <see cref="IOp.ComputeSelfCost"/>, but a metadata provider
        /// can override this with its own cost model.
        /// </summary>
        /// <returns>The estimated cost, or <c>null</c> if no reliable estimate can be determined.</returns>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.NonCumulativeCost", "getNonCumulativeCost()")]
        IOpCost? GetNonCumulativeCost();

        /// <summary>
        /// The handler API for <see cref="NonCumulativeCost"/> metadata.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.NonCumulativeCost.Handler")]
        public interface Handler : IMetadataHandler<NonCumulativeCost>
        {
            /// <summary>
            /// Estimates the non-cumulative cost of <paramref name="op"/>.
            /// </summary>
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

        /// <summary>
        /// The definition of this metadata kind.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory", "DEF")]
        public static readonly MetadataDef<Memory> Def = MetadataDef<Memory>.Of(typeof(Memory), typeof(Handler));

        // Calcite also declares the accessor `memory()`; C# forbids a member named the same as its
        // enclosing type, so it is omitted here. (These accessors are the old metadata-proxy API and are
        // unused by the explicit-registry dispatch; the Handler below carries the real port.)

        /// <summary>
        /// Returns the cumulative amount of memory, in bytes, required by the physical operator implementing
        /// this op, and all other operators within the same phase, across all splits.
        /// </summary>
        /// <seealso cref="Parallelism.SplitCount()"/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory", "cumulativeMemoryWithinPhase()")]
        double? CumulativeMemoryWithinPhase();

        /// <summary>
        /// Returns the expected cumulative amount of memory, in bytes, required by the physical operator
        /// implementing this op, and all operators within the same phase, within each split. Basic formula:
        /// <c>cumulativeMemoryWithinPhaseSplit = cumulativeMemoryWithinPhase / Parallelism.splitCount</c>.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory", "cumulativeMemoryWithinPhaseSplit()")]
        double? CumulativeMemoryWithinPhaseSplit();

        /// <summary>
        /// The handler API for <see cref="Memory"/> metadata.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory.Handler")]
        public interface Handler : IMetadataHandler<Memory>
        {
            /// <summary>
            /// Returns the expected amount of memory, in bytes, required by a physical operator implementing
            /// <paramref name="op"/>, across all splits. How much memory is used depends very much on the
            /// algorithm.
            /// </summary>
            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory.Handler", "memory(RelNode, RelMetadataQuery)")]
            double? Memory(IOp op, OpMetadataQuery mq);

            /// <summary>
            /// Returns the cumulative memory of <paramref name="op"/> within its phase, across all splits.
            /// </summary>
            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Memory.Handler", "cumulativeMemoryWithinPhase(RelNode, RelMetadataQuery)")]
            double? CumulativeMemoryWithinPhase(IOp op, OpMetadataQuery mq);

            /// <summary>
            /// Returns the cumulative memory of <paramref name="op"/> within its phase, within each split.
            /// </summary>
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

        /// <summary>
        /// The definition of this metadata kind.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism", "DEF")]
        public static readonly MetadataDef<Parallelism> Def = MetadataDef<Parallelism>.Of(typeof(Parallelism), typeof(Handler));

        /// <summary>
        /// Returns whether each physical operator implementing this op belongs to a different process than
        /// its inputs. A collection of operators processing all of the splits of a particular stage in the
        /// pipeline is called a "phase"; a phase starts with a leaf op, or with a phase-change op (such as
        /// an exchange that causes data to be sent across the network).
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism", "isPhaseTransition()")]
        bool? IsPhaseTransition();

        /// <summary>
        /// Returns the number of distinct splits of the data. Note that splits must be distinct: for
        /// broadcast, where each copy is the same, this returns 1.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism", "splitCount()")]
        int? SplitCount();

        /// <summary>
        /// The handler API for <see cref="Parallelism"/> metadata.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism.Handler")]
        public interface Handler : IMetadataHandler<Parallelism>
        {
            /// <summary>
            /// Returns whether <paramref name="op"/> is a phase transition.
            /// </summary>
            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.Parallelism.Handler", "isPhaseTransition(RelNode, RelMetadataQuery)")]
            bool? IsPhaseTransition(IOp op, OpMetadataQuery mq);

            /// <summary>
            /// Returns the number of distinct splits of <paramref name="op"/>'s data.
            /// </summary>
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

        /// <summary>
        /// The definition of this metadata kind.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.LowerBoundCost", "DEF")]
        public static readonly MetadataDef<LowerBoundCost> Def = MetadataDef<LowerBoundCost>.Of(typeof(LowerBoundCost), typeof(Handler));

        /// <summary>
        /// Returns the lower bound cost of an op under <paramref name="planner"/>.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.LowerBoundCost", "getLowerBoundCost(VolcanoPlanner)")]
        IOpCost? GetLowerBoundCost(VolcanoPlanner planner);

        /// <summary>
        /// The handler API for <see cref="LowerBoundCost"/> metadata.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.LowerBoundCost.Handler")]
        public interface Handler : IMetadataHandler<LowerBoundCost>
        {
            /// <summary>
            /// Returns the lower bound cost of <paramref name="op"/> under <paramref name="planner"/>.
            /// </summary>
            [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.BuiltInMetadata.LowerBoundCost.Handler", "getLowerBoundCost(RelNode, RelMetadataQuery, VolcanoPlanner)")]
            IOpCost? GetLowerBoundCost(IOp op, OpMetadataQuery mq, VolcanoPlanner planner);
        }

    }

}
