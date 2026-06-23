using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Hep;

/// <summary>
/// Wraps a real node as a vertex in a DAG representing the whole plan. Several parents can reference
/// one vertex (sharing common subexpressions), and replacing the wrapped node is seen by all of them.
/// </summary>
/// <remarks>
/// A vertex has no children of its own; the graph edges are the vertices that appear among
/// <see cref="CurrentNode"/>'s children. The heuristic planner's matching sees through a vertex to its
/// current node.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex")]
sealed class HepNodeVertex : AbstractNode
{

    INode _currentNode;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "HepRelVertex(RelNode)")]
    public HepNodeVertex(INode currentNode)
        : base(currentNode.Cluster, currentNode.Traits, ImmutableArray<INode>.Empty)
    {
        _currentNode = currentNode;
    }

    /// <summary>
    /// The node currently chosen as the implementation of this vertex.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "getCurrentRel()")]
    public INode CurrentNode => _currentNode;

    /// <summary>
    /// This vertex with its wrapping stripped away — its current node.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "stripped()")]
    public INode Stripped => _currentNode;

    /// <summary>
    /// Replaces the implementation for this vertex with a new node.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "replaceRel(RelNode)")]
    public void ReplaceNode(INode newNode)
    {
        _currentNode = newNode;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "copy(RelTraitSet, List<RelNode>)")]
    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return this;
    }

    /// <summary>
    /// A vertex explains itself in terms of the node it currently stands in for.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "explain(RelWriter)")]
    public override INodeWriter ExplainTerms(INodeWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("current", _currentNode);
        return writer;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "deepEquals(Object)")]
    public override bool DeepEquals(INode? other)
    {
        return ReferenceEquals(this, other)
            || (other is HepNodeVertex vertex && ReferenceEquals(_currentNode, vertex._currentNode));
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepRelVertex", "deepHashCode()")]
    public override int DeepHashCode()
    {
        return _currentNode.GetHashCode();
    }

}
