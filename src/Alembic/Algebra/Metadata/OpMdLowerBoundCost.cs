using Alembic.Plan;
using Alembic.Plan.Volcano;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// Handler for <see cref="BuiltInMetadata.LowerBoundCost"/>. A subset's lower bound is its winner cost
/// (once it is physical); any other op's is its own cost plus the lower bounds of its inputs. The two
/// typed methods are the per-op-type overloads the reflective provider routes between.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdLowerBoundCost")]
public class OpMdLowerBoundCost : IMetadataHandler<BuiltInMetadata.LowerBoundCost>
{
    /// <summary>
    /// The provider that exposes this handler to the metadata system.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdLowerBoundCost", "SOURCE")]
    public static readonly IOpMetadataProvider Source =
        ReflectiveOpMetadataProvider.ReflectiveSource(new OpMdLowerBoundCost(), typeof(BuiltInMetadata.LowerBoundCost.Handler));

    /// <summary>
    /// Initializes the handler. Use <see cref="Source"/> to obtain the provider.
    /// </summary>
    protected OpMdLowerBoundCost()
    {
    }

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdLowerBoundCost", "getDef()")]
    public MetadataDef<BuiltInMetadata.LowerBoundCost> GetDef() => BuiltInMetadata.LowerBoundCost.Def;

    /// <summary>
    /// The lower bound of a subset: its winner cost once it is physical, or <c>null</c> while it is still
    /// logical.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdLowerBoundCost", "getLowerBoundCost(RelSubset, RelMetadataQuery, VolcanoPlanner)")]
    public IOpCost? GetLowerBoundCost(OpSubset subset, OpMetadataQuery mq, VolcanoPlanner planner)
    {
        if (planner.IsLogical(subset))
            return null;

        return subset.GetWinnerCost();
    }

    /// <summary>
    /// The lower bound of any other op: its own non-cumulative cost plus the lower bounds of its inputs
    /// (an infinite self-cost is treated as unknown). Returns <c>null</c> while the op is still logical.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdLowerBoundCost", "getLowerBoundCost(RelNode, RelMetadataQuery, VolcanoPlanner)")]
    public IOpCost? GetLowerBoundCost(IOp op, OpMetadataQuery mq, VolcanoPlanner planner)
    {
        if (planner.IsLogical(op))
            return null;

        var selfCost = mq.GetNonCumulativeCost(op);
        if (selfCost is not null && selfCost.IsInfinite)
            selfCost = null;

        foreach (var input in op.Children)
        {
            var lb = mq.GetLowerBoundCost(input, planner);
            if (lb is not null)
                selfCost = selfCost is null ? lb : selfCost.Plus(lb);
        }

        return selfCost;
    }
}
