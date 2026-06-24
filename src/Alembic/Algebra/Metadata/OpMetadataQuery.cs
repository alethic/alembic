using Alembic.Plan;
using Alembic.Plan.Volcano;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// The entry point for asking an op for its metadata. Holds one handler per kind and exposes one method
/// per kind, each running through the per-query cache in <see cref="OpMetadataQueryBase"/>. New metadata
/// kinds are added by writing a handler and a method here (or on a subclass).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery")]
public class OpMetadataQuery : OpMetadataQueryBase
{

    // Per-kind cache keys (each metadata kind needs a distinct key for the same op). The lower-bound
    // kind's planner argument is constant within a query, so it need not enter the key.
    static readonly object CumulativeCostKey = new object();
    static readonly object NonCumulativeCostKey = new object();
    static readonly object LowerBoundCostKey = new object();

    readonly BuiltInMetadata.CumulativeCost.Handler _cumulativeCost = new OpMdCumulativeCost();
    readonly BuiltInMetadata.NonCumulativeCost.Handler _nonCumulativeCost = new OpMdNonCumulativeCost();
    readonly BuiltInMetadata.LowerBoundCost.Handler _lowerBoundCost = new OpMdLowerBoundCost();

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "getCumulativeCost(RelNode)")]
    public IOpCost? GetCumulativeCost(IOp op)
    {
        op = Delegate(op);
        return Cache(op, CumulativeCostKey, () => _cumulativeCost.GetCumulativeCost(op, this));
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "getNonCumulativeCost(RelNode)")]
    public IOpCost? GetNonCumulativeCost(IOp op)
    {
        op = Delegate(op);
        return Cache(op, NonCumulativeCostKey, () => _nonCumulativeCost.GetNonCumulativeCost(op, this));
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMetadataQuery", "getLowerBoundCost(RelNode, VolcanoPlanner)")]
    public IOpCost? GetLowerBoundCost(IOp op, VolcanoPlanner planner)
    {
        op = Delegate(op);
        return Cache(op, LowerBoundCostKey, () => _lowerBoundCost.GetLowerBoundCost(op, this, planner));
    }

}
