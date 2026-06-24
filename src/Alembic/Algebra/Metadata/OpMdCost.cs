using Alembic.Plan;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// Handler for <see cref="BuiltInMetadata.CumulativeCost"/>: an op's cost plus the cumulative cost of
/// its inputs.
/// </summary>
/// <remarks>
/// In Calcite the cumulative/non-cumulative cost handlers live on <c>RelMdPercentageOriginalRows</c>
/// (a relational provider); Alembic keeps only the cost methods, in their own cost-only handlers.
/// </remarks>
public sealed class OpMdCumulativeCost : BuiltInMetadata.CumulativeCost.Handler
{
    delegate IOpCost? Impl(IOp op, OpMetadataQuery mq);

    readonly MetadataRegistry<Impl> _registry = new MetadataRegistry<Impl>();

    public OpMdCumulativeCost()
    {
        _registry.RegisterDefault((op, mq) =>
        {
            var cost = mq.GetNonCumulativeCost(op);
            if (cost is null)
                return null;

            foreach (var input in op.Children)
            {
                var inputCost = mq.GetCumulativeCost(input);
                if (inputCost is null)
                    return null;

                cost = cost.Plus(inputCost);
            }

            return cost;
        });
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdPercentageOriginalRows", "getCumulativeCost(RelNode, RelMetadataQuery)")]
    public IOpCost? GetCumulativeCost(IOp op, OpMetadataQuery mq) => _registry.Resolve(op)(op, mq);
}

/// <summary>
/// Handler for <see cref="BuiltInMetadata.NonCumulativeCost"/>: an op's own cost, asked of the op via
/// <see cref="IOp.ComputeSelfCost"/>.
/// </summary>
public sealed class OpMdNonCumulativeCost : BuiltInMetadata.NonCumulativeCost.Handler
{
    delegate IOpCost? Impl(IOp op, OpMetadataQuery mq);

    readonly MetadataRegistry<Impl> _registry = new MetadataRegistry<Impl>();

    public OpMdNonCumulativeCost()
    {
        // Calcite: rel.computeSelfCost(rel.getCluster().getPlanner(), mq).
        _registry.RegisterDefault((op, mq) => op.ComputeSelfCost(op.Cluster.Planner, mq));
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdPercentageOriginalRows", "getNonCumulativeCost(RelNode, RelMetadataQuery)")]
    public IOpCost? GetNonCumulativeCost(IOp op, OpMetadataQuery mq) => _registry.Resolve(op)(op, mq);
}
