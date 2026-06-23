using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Hep;

/// <summary>
/// Wraps a real op as a vertex in a DAG representing the whole plan. Several parents can reference
/// one vertex (sharing common subexpressions), and replacing the wrapped op is seen by all of them.
/// </summary>
/// <remarks>
/// A vertex has no children of its own; the graph edges are the vertices that appear among
/// <see cref="CurrentOp"/>'s children. The heuristic planner's matching sees through a vertex to its
/// current op.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex")]
sealed class HepOpVertex : AbstractOp
{

    IOpNode _currentOp;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "HepRelVertex(RelNode)")]
    public HepOpVertex(IOpNode currentOp)
        : base(currentOp.Cluster, currentOp.Traits, ImmutableArray<IOpNode>.Empty)
    {
        _currentOp = currentOp;
    }

    /// <summary>
    /// The op currently chosen as the implementation of this vertex.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "getCurrentRel()")]
    public IOpNode CurrentOp => _currentOp;

    /// <summary>
    /// This vertex with its wrapping stripped away — its current op.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "stripped()")]
    public IOpNode Stripped => _currentOp;

    /// <summary>
    /// Replaces the implementation for this vertex with a new op.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "replaceRel(RelNode)")]
    public void ReplaceOp(IOpNode newOp)
    {
        _currentOp = newOp;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "copy(RelTraitSet, List<RelNode>)")]
    public override IOpNode Copy(OpTraitSet traits, ImmutableArray<IOpNode> children)
    {
        return this;
    }

    /// <summary>
    /// A vertex explains itself in terms of the op it currently stands in for.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "explain(RelWriter)")]
    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("current", _currentOp);
        return writer;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "deepEquals(Object)")]
    public override bool DeepEquals(IOpNode? other)
    {
        return ReferenceEquals(this, other)
            || (other is HepOpVertex vertex && ReferenceEquals(_currentOp, vertex._currentOp));
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "deepHashCode()")]
    public override int DeepHashCode()
    {
        return _currentOp.GetHashCode();
    }

}
