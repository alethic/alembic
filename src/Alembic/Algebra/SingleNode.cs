using System.Collections.Immutable;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// Convenience base for an <see cref="INode"/> with exactly one child. It lists the child as an input
/// term; subclasses add their own attributes in <see cref="AbstractNode.ExplainTerms"/> and override
/// <see cref="INode.Copy"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel")]
public abstract class SingleNode : AbstractNode
{

    /// <summary>
    /// Initializes the node with its traits and single child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "SingleRel(RelOptCluster, RelTraitSet, RelNode)")]
    protected SingleNode(TraitSet traits, INode child)
        : base(child.Cluster, traits, ImmutableArray.Create(child))
    {

    }

    /// <summary>
    /// This node's single child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "getInput()")]
    public INode Child => Children[0];

    /// <inheritdoc />
    public override INodeWriter ExplainTerms(INodeWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("input", Child);
        return writer;
    }

}
