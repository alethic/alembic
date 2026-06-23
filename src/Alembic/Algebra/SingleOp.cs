using System.Collections.Immutable;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// Convenience base for an <see cref="IOpNode"/> with exactly one child. It lists the child as an input
/// term; subclasses add their own attributes in <see cref="AbstractOp.ExplainTerms"/> and override
/// <see cref="IOpNode.Copy"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel")]
public abstract class SingleOp : AbstractOp
{

    /// <summary>
    /// Initializes the op with its traits and single child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "SingleRel(RelOptCluster, RelTraitSet, RelNode)")]
    protected SingleOp(TraitSet traits, IOpNode child)
        : base(child.Cluster, traits, ImmutableArray.Create(child))
    {

    }

    /// <summary>
    /// This op's single child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.SingleRel", "getInput()")]
    public IOpNode Child => Children[0];

    /// <inheritdoc />
    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("input", Child);
        return writer;
    }

}
