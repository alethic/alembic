using System.Collections.Immutable;
using System.Diagnostics;

using Alembic.Algebra.Metadata;
using Alembic.Plan;

namespace Alembic.Algebra.Convert;

/// <summary>
/// Abstract base for an <see cref="IConverter"/>: a single-input op that records the input's traits
/// and the dimension it converts.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterImpl")]
public abstract class ConverterImpl : AbstractOp, IConverter
{

    IOp _input;

    /// <summary>
    /// Creates a converter producing <paramref name="traits"/> from <paramref name="child"/>, modifying
    /// the dimension <paramref name="traitDef"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterImpl", "ConverterImpl(RelOptCluster, RelTraitDef, RelTraitSet, RelNode)")]
    protected ConverterImpl(OpCluster cluster, OpTraitDef? traitDef, OpTraitSet traits, IOp child)
        : base(cluster, traits)
    {
        _input = child;
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
    public IOp Input => _input;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "getInputs()")]
    public override ImmutableArray<IOp> Inputs => [_input];

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "deriveRowType()")]
    protected override IOutputType DeriveOutputType() => _input.OutputType;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "replaceInput(int, RelNode)")]
    public override void ReplaceInput(int ordinalInParent, IOp p)
    {
        Debug.Assert(ordinalInParent == 0);
        _input = p;
        RecomputeDigest();
    }

    /// <inheritdoc />
    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("input", _input);
        return writer;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterImpl", "computeSelfCost(RelOptPlanner, RelMetadataQuery)")]
    public override IOpCost? ComputeSelfCost(IOpPlanner planner, OpMetadataQuery mq)
    {
        return planner.CostFactory.MakeCost(0, 0);
    }

}
