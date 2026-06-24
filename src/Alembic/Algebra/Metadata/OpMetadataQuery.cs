using Alembic.Plan;
using Alembic.Plan.Volcano;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// The entry point for asking an op for its metadata. Holds one handler dispatcher per kind — obtained
/// from a <see cref="IMetadataHandlerProvider"/>, which builds each by reflection over the registered
/// handlers — and exposes one method per kind, each running through the per-query cache in
/// <see cref="OpMetadataQueryBase"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery")]
public class OpMetadataQuery : OpMetadataQueryBase
{
    // The default provider: a proxying (reflective) handler provider over the central handler list.
    static readonly IMetadataHandlerProvider DefaultProvider = new ProxyingMetadataHandlerProvider(DefaultOpMetadataProvider.Instance);

    // Per-kind cache keys (each metadata kind needs a distinct key for the same op). The lower-bound
    // kind's planner argument is constant within a query, so it need not enter the key.
    static readonly object CumulativeCostKey = new object();
    static readonly object NonCumulativeCostKey = new object();
    static readonly object LowerBoundCostKey = new object();
    static readonly object MemoryKey = new object();
    static readonly object CumulativeMemoryWithinPhaseKey = new object();
    static readonly object CumulativeMemoryWithinPhaseSplitKey = new object();
    static readonly object IsPhaseTransitionKey = new object();
    static readonly object SplitCountKey = new object();

    readonly BuiltInMetadata.CumulativeCost.Handler _cumulativeCost;
    readonly BuiltInMetadata.NonCumulativeCost.Handler _nonCumulativeCost;
    readonly BuiltInMetadata.LowerBoundCost.Handler _lowerBoundCost;
    readonly BuiltInMetadata.Memory.Handler _memory;
    readonly BuiltInMetadata.Parallelism.Handler _parallelism;

    /// <summary>
    /// Creates a query backed by the default handler provider over the central handler list.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "RelMetadataQuery()")]
    public OpMetadataQuery()
        : this(DefaultProvider)
    {
    }

    /// <summary>
    /// Creates a query whose handlers are obtained from <paramref name="provider"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "RelMetadataQuery(MetadataHandlerProvider)")]
    public OpMetadataQuery(IMetadataHandlerProvider provider)
    {
        _cumulativeCost = provider.Handler<BuiltInMetadata.CumulativeCost.Handler>();
        _nonCumulativeCost = provider.Handler<BuiltInMetadata.NonCumulativeCost.Handler>();
        _lowerBoundCost = provider.Handler<BuiltInMetadata.LowerBoundCost.Handler>();
        _memory = provider.Handler<BuiltInMetadata.Memory.Handler>();
        _parallelism = provider.Handler<BuiltInMetadata.Parallelism.Handler>();
    }

    /// <summary>
    /// Returns the cumulative cost of <paramref name="op"/> (its cost including all of its inputs).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "getCumulativeCost(RelNode)")]
    public IOpCost? GetCumulativeCost(IOp op)
    {
        op = Delegate(op);
        return Cache(op, CumulativeCostKey, () => _cumulativeCost.GetCumulativeCost(op, this));
    }

    /// <summary>
    /// Returns the non-cumulative cost of <paramref name="op"/> (its own cost, not counting its inputs).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "getNonCumulativeCost(RelNode)")]
    public IOpCost? GetNonCumulativeCost(IOp op)
    {
        op = Delegate(op);
        return Cache(op, NonCumulativeCostKey, () => _nonCumulativeCost.GetNonCumulativeCost(op, this));
    }

    /// <summary>
    /// Returns the lower bound cost of <paramref name="op"/> under <paramref name="planner"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "getLowerBoundCost(RelNode, VolcanoPlanner)")]
    public IOpCost? GetLowerBoundCost(IOp op, VolcanoPlanner planner)
    {
        op = Delegate(op);
        return Cache(op, LowerBoundCostKey, () => _lowerBoundCost.GetLowerBoundCost(op, this, planner));
    }

    /// <summary>
    /// Returns the memory, in bytes, used by <paramref name="op"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "memory(RelNode)")]
    public double? Memory(IOp op)
    {
        op = Delegate(op);
        return Cache(op, MemoryKey, () => _memory.Memory(op, this));
    }

    /// <summary>
    /// Returns the cumulative memory of <paramref name="op"/> within its phase, across all splits.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "cumulativeMemoryWithinPhase(RelNode)")]
    public double? CumulativeMemoryWithinPhase(IOp op)
    {
        op = Delegate(op);
        return Cache(op, CumulativeMemoryWithinPhaseKey, () => _memory.CumulativeMemoryWithinPhase(op, this));
    }

    /// <summary>
    /// Returns the cumulative memory of <paramref name="op"/> within its phase, within each split.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "cumulativeMemoryWithinPhaseSplit(RelNode)")]
    public double? CumulativeMemoryWithinPhaseSplit(IOp op)
    {
        op = Delegate(op);
        return Cache(op, CumulativeMemoryWithinPhaseSplitKey, () => _memory.CumulativeMemoryWithinPhaseSplit(op, this));
    }

    /// <summary>
    /// Returns whether <paramref name="op"/> is a phase transition.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "isPhaseTransition(RelNode)")]
    public bool? IsPhaseTransition(IOp op)
    {
        op = Delegate(op);
        return Cache(op, IsPhaseTransitionKey, () => _parallelism.IsPhaseTransition(op, this));
    }

    /// <summary>
    /// Returns the number of distinct splits of <paramref name="op"/>'s data.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "splitCount(RelNode)")]
    public int? SplitCount(IOp op)
    {
        op = Delegate(op);
        return Cache(op, SplitCountKey, () => _parallelism.SplitCount(op, this));
    }
}
