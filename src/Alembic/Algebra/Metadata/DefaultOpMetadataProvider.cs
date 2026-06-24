namespace Alembic.Algebra.Metadata;

/// <summary>
/// The central provider: the explicit list of every metadata handler Alembic ships. Handlers are
/// discovered the same way Calcite discovers them — by being named here, not by any auto-scan.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.DefaultRelMetadataProvider")]
public sealed class DefaultOpMetadataProvider : ChainedOpMetadataProvider
{
    /// <summary>
    /// The shared instance.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.DefaultRelMetadataProvider", "INSTANCE")]
    public static readonly DefaultOpMetadataProvider Instance = new DefaultOpMetadataProvider();

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.DefaultRelMetadataProvider", "DefaultRelMetadataProvider()")]
    DefaultOpMetadataProvider()
        : base(new IOpMetadataProvider[]
        {
            OpMdParallelism.Source,
            OpMdLowerBoundCost.Source,
            OpMdMemory.Source,
            OpMdCumulativeCost.Source,
            OpMdNonCumulativeCost.Source,
        })
    {
    }
}
