using System.Collections.Immutable;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// A node in a plan. Alembic attaches no meaning to a node; the user supplies the
/// concrete types and the rules that rewrite them.
/// </summary>
/// <remarks>
/// Nodes are immutable. The planner rewrites by producing new nodes via <see cref="Copy"/>,
/// sharing the subtrees it does not touch. A node's own <c>Equals</c>/<c>GetHashCode</c> are
/// left as reference identity; structural equivalence — the value the planner deduplicates on —
/// is the separate <see cref="DeepEquals"/> / <see cref="DeepHashCode"/> contract.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode")]
public interface INode
{

    /// <summary>
    /// The cluster this node belongs to — its shared planning context. Every node in a plan shares one.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getCluster()")]
    Cluster Cluster { get; }

    /// <summary>
    /// The physical properties (convention, etc.) carried by this node.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getTraitSet()")]
    TraitSet Traits { get; }

    /// <summary>
    /// This node's child nodes, in order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getInputs()")]
    ImmutableArray<INode> Children { get; }

    /// <summary>
    /// Produces a copy of this node with the given traits and children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "copy(RelTraitSet, List<RelNode>)")]
    INode Copy(TraitSet traits, ImmutableArray<INode> children);

    /// <summary>
    /// Whether this node is structurally equivalent to <paramref name="other"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "deepEquals(Object)")]
    bool DeepEquals(INode? other);

    /// <summary>
    /// A hash consistent with <see cref="DeepEquals"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "deepHashCode()")]
    int DeepHashCode();

    /// <summary>
    /// Describes this node to <paramref name="writer"/> — its attributes and inputs. The plan-rendering
    /// and digest machinery drive this.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "explain(RelWriter)")]
    void Explain(INodeWriter writer);

    /// <summary>
    /// This node's structural digest — the key the planner deduplicates on.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getRelDigest()")]
    INodeDigest GetDigest()
    {
        return new NodeDigest(this);
    }

    /// <summary>
    /// Recomputes this node's digest, discarding any cached value. A planner calls this after something
    /// a node's digest depends on has changed underneath it (e.g. a shared child's content).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "recomputeDigest()")]
    void RecomputeDigest()
    {
        GetDigest().Clear();
    }

    /// <summary>
    /// This node's convention.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getConvention()")]
    IConvention Convention
    {
        get { return Traits.Convention; }
    }

    /// <summary>
    /// Whether this node only enforces a physical property on its input (e.g. a converter) rather than
    /// computing a result. The top-down search gives enforcers lower priority. Defaults to <c>false</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "isEnforcer()")]
    bool IsEnforcer => false;

    /// <summary>
    /// This node with any planner wrapping removed — a placeholder node (an equivalence subset, a graph
    /// vertex) returns the concrete node it stands for; an ordinary node returns itself.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "stripped()")]
    INode Stripped => this;

    /// <summary>
    /// This node's own cost, not counting its inputs. A cost-based planner consults it; a heuristic
    /// planner ignores it. The default is a small positive cost, so a node opts into a real cost model
    /// only by overriding this.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "computeSelfCost(RelOptPlanner, RelMetadataQuery)")]
    ICost ComputeSelfCost(IPlanner planner)
    {
        return planner.CostFactory.MakeTinyCost();
    }

}
