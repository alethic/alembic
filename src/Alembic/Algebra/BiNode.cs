using System.Collections.Immutable;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// Convenience base for an <see cref="INode"/> with exactly two children — a left and a right. It
/// lists both as input terms; subclasses add their own attributes in <see cref="AbstractNode.ExplainTerms"/>
/// and override <see cref="INode.Copy"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel")]
public abstract class BiNode : AbstractNode
{

    /// <summary>
    /// Initializes the node with its traits and its left and right children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel", "BiRel(RelOptCluster, RelTraitSet, RelNode, RelNode)")]
    protected BiNode(TraitSet traits, INode left, INode right)
        : base(left.Cluster, traits, ImmutableArray.Create(left, right))
    {

    }

    /// <summary>
    /// This node's left child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel", "getLeft()")]
    public INode Left => Children[0];

    /// <summary>
    /// This node's right child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.BiRel", "getRight()")]
    public INode Right => Children[1];

    /// <inheritdoc />
    public override INodeWriter ExplainTerms(INodeWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("left", Left);
        writer.Input("right", Right);
        return writer;
    }

}
