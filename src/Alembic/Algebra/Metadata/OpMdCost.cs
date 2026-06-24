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
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdPercentageOriginalRows")]
public class OpMdCumulativeCost : IMetadataHandler<BuiltInMetadata.CumulativeCost>
{
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdPercentageOriginalRows", "SOURCE")]
    public static readonly IOpMetadataProvider Source =
        ReflectiveOpMetadataProvider.ReflectiveSource(new OpMdCumulativeCost(), typeof(BuiltInMetadata.CumulativeCost.Handler));

    protected OpMdCumulativeCost()
    {
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdPercentageOriginalRows", "getDef()")]
    public MetadataDef<BuiltInMetadata.CumulativeCost> GetDef() => BuiltInMetadata.CumulativeCost.Def;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdPercentageOriginalRows", "getCumulativeCost(RelNode, RelMetadataQuery)")]
    public IOpCost? GetCumulativeCost(IOp op, OpMetadataQuery mq)
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
    }
}

/// <summary>
/// Handler for <see cref="BuiltInMetadata.NonCumulativeCost"/>: an op's own cost, asked of the op via
/// <see cref="IOp.ComputeSelfCost"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdPercentageOriginalRows")]
public class OpMdNonCumulativeCost : IMetadataHandler<BuiltInMetadata.NonCumulativeCost>
{
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdPercentageOriginalRows", "SOURCE")]
    public static readonly IOpMetadataProvider Source =
        ReflectiveOpMetadataProvider.ReflectiveSource(new OpMdNonCumulativeCost(), typeof(BuiltInMetadata.NonCumulativeCost.Handler));

    protected OpMdNonCumulativeCost()
    {
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdPercentageOriginalRows", "getDef()")]
    public MetadataDef<BuiltInMetadata.NonCumulativeCost> GetDef() => BuiltInMetadata.NonCumulativeCost.Def;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdPercentageOriginalRows", "getNonCumulativeCost(RelNode, RelMetadataQuery)")]
    public IOpCost? GetNonCumulativeCost(IOp op, OpMetadataQuery mq)
        => op.ComputeSelfCost(op.Cluster.Planner, mq);
}
