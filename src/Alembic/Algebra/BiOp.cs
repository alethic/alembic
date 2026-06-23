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

    /// <summary>
    /// Initializes the op with its traits and its left and right children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel", "BiRel(RelOptCluster, RelTraitSet, RelNode, RelNode)")]
    protected BiOp(OpTraitSet traits, IOp left, IOp right)
        : base(left.Cluster, traits, ImmutableArray.Create(left, right))
    {

    }

    /// <summary>
    /// This op's left child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel", "getLeft()")]
    public IOp Left => Children[0];

    /// <summary>
    /// This op's right child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel", "getRight()")]
    public IOp Right => Children[1];

    /// <inheritdoc />
    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("left", Left);
        writer.Input("right", Right);
        return writer;
    }

}
