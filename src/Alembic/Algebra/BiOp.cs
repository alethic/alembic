using System;
using System.Collections.Immutable;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// Convenience base for an <see cref="IOp"/> with exactly two children — a left and a right. It
/// lists both as input terms; subclasses add their own attributes in <see cref="AbstractOp.ExplainTerms"/>
/// and override <see cref="IOp.Copy"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel")]
public abstract class BiOp : AbstractOp
{

    IOp _left;
    IOp _right;

    /// <summary>
    /// Initializes the op with its traits and its left and right children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel", "BiRel(RelOptCluster, RelTraitSet, RelNode, RelNode)")]
    protected BiOp(OpCluster cluster, OpTraitSet traits, IOp left, IOp right)
        : base(cluster, traits)
    {
        _left = left;
        _right = right;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel", "getInputs()")]
    public override ImmutableArray<IOp> Inputs => [_left, _right];

    /// <summary>
    /// This op's left child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel", "getLeft()")]
    public IOp Left => _left;

    /// <summary>
    /// This op's right child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel", "getRight()")]
    public IOp Right => _right;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel", "replaceInput(int, RelNode)")]
    public override void ReplaceInput(int ordinalInParent, IOp p)
    {
        switch (ordinalInParent)
        {
            case 0:
                _left = p;
                break;
            case 1:
                _right = p;
                break;
            default:
                throw new IndexOutOfRangeException("Input " + ordinalInParent);
        }

        RecomputeDigest();
    }

    /// <inheritdoc />
    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("left", Left);
        writer.Input("right", Right);
        return writer;
    }

}
