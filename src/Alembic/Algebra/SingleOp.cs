using System.Collections.Immutable;
using System.Diagnostics;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// Convenience base for an <see cref="IOp"/> with exactly one child. It lists the child as an input
/// term; subclasses add their own attributes in <see cref="AbstractOp.ExplainTerms"/> and override
/// <see cref="IOp.Copy"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel")]
public abstract class SingleOp : AbstractOp
{

    IOp _input;

    /// <summary>
    /// Initializes the op with its traits and single child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "SingleRel(RelOptCluster, RelTraitSet, RelNode)")]
    protected SingleOp(OpCluster cluster, OpTraitSet traits, IOp child)
        : base(cluster, traits)
    {
        _input = child;
    }

    /// <summary>
    /// This op's single child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "getInput()")]
    public IOp Child => _input;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "getInputs()")]
    public override ImmutableArray<IOp> Inputs => ImmutableArray.Create(_input);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "deriveRowType()")]
    protected override IOutputType DeriveOutputType() => _input.OutputType;

    /// <inheritdoc />
    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("input", Child);
        return writer;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "replaceInput(int, RelNode)")]
    public override void ReplaceInput(int ordinalInParent, IOp rel)
    {
        Debug.Assert(ordinalInParent == 0);
        _input = rel;
        RecomputeDigest();
    }

}
