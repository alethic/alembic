using Alembic.Plan;
using Alembic.Plan.Volcano;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// Handler for <see cref="BuiltInMetadata.LowerBoundCost"/>. A subset's lower bound is its winner cost
/// (once it is physical); any other op's is its own cost plus the lower bounds of its inputs.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdLowerBoundCost")]
public sealed class OpMdLowerBoundCost : BuiltInMetadata.LowerBoundCost.Handler
{
    delegate IOpCost? Impl(IOp op, OpMetadataQuery mq, VolcanoPlanner planner);

    readonly MetadataRegistry<Impl> _registry = new MetadataRegistry<Impl>();

    public OpMdLowerBoundCost()
    {
        _registry.Register(typeof(OpSubset), (op, mq, planner) =>
        {
            var subset = (OpSubset)op;
            if (planner.IsLogical(subset))
                return null;

            return subset.GetWinnerCost();
        });

        _registry.RegisterDefault((op, mq, planner) =>
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
        });
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdLowerBoundCost", "getLowerBoundCost(RelNode, RelMetadataQuery, VolcanoPlanner)")]
    public IOpCost? GetLowerBoundCost(IOp op, OpMetadataQuery mq, VolcanoPlanner planner)
        => _registry.Resolve(op)(op, mq, planner);
}
