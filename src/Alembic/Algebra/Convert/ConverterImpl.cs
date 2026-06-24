using Alembic.Algebra.Metadata;
using Alembic.Plan;

namespace Alembic.Algebra.Convert;

/// <summary>
/// Abstract base for an <see cref="IConverter"/>: a single-input op that records the input's traits
/// and the dimension it converts.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterImpl")]
public abstract class ConverterImpl : SingleOp, IConverter
{

    /// <summary>
    /// Creates a converter producing <paramref name="traits"/> from <paramref name="child"/>, modifying
    /// the dimension <paramref name="traitDef"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterImpl", "ConverterImpl(RelOptCluster, RelTraitDef, RelTraitSet, RelNode)")]
    protected ConverterImpl(OpCluster cluster, OpTraitDef? traitDef, OpTraitSet traits, IOp child)
        : base(cluster, traits, child)
    {
        InputTraits = child.Traits;
        TraitDef = traitDef;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getInputTraits()")]
    public OpTraitSet InputTraits { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getTraitDef()")]
    public OpTraitDef? TraitDef { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getInput()")]
    public IOp Input => Child;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterImpl", "computeSelfCost(RelOptPlanner, RelMetadataQuery)")]
    public override IOpCost? ComputeSelfCost(IOpPlanner planner, OpMetadataQuery mq)
    {
        return planner.CostFactory.MakeCost(0, 0);
    }

}
