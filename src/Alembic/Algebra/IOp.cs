using System.Collections.Immutable;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// An op in a plan. Alembic attaches no meaning to an op; the user supplies the
/// concrete types and the rules that rewrite them.
/// </summary>
/// <remarks>
/// Ops are immutable. The planner rewrites by producing new ops via <see cref="Copy"/>,
/// sharing the subtrees it does not touch. An op's own <c>Equals</c>/<c>GetHashCode</c> are
/// left as reference identity; structural equivalence — the value the planner deduplicates on —
/// is the separate <see cref="DeepEquals"/> / <see cref="DeepHashCode"/> contract.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode")]
public interface IOp
{

    /// <summary>
    /// The cluster this op belongs to — its shared planning context. Every op in a plan shares one.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getCluster()")]
    OpCluster Cluster { get; }

    /// <summary>
    /// The physical properties (convention, etc.) carried by this op.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getTraitSet()")]
    OpTraitSet Traits { get; }

    /// <summary>
    /// This op's child ops, in order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getInputs()")]
    ImmutableArray<IOp> Children { get; }

    /// <summary>
    /// Produces a copy of this op with the given traits and children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "copy(RelTraitSet, List<RelNode>)")]
    IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children);

    /// <summary>
    /// Whether this op is structurally equivalent to <paramref name="other"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "deepEquals(Object)")]
    bool DeepEquals(IOp? other);

    /// <summary>
    /// A hash consistent with <see cref="DeepEquals"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "deepHashCode()")]
    int DeepHashCode();

    /// <summary>
    /// Describes this op to <paramref name="writer"/> — its attributes and inputs. The plan-rendering
    /// and digest machinery drive this.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "explain(RelWriter)")]
    void Explain(IOpWriter writer);

    /// <summary>
    /// This op's structural digest — the key the planner deduplicates on.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getRelDigest()")]
    IOpDigest GetDigest()
    {
        return new OpDigest(this);
    }

    /// <summary>
    /// Recomputes this op's digest, discarding any cached value. A planner calls this after something
    /// an op's digest depends on has changed underneath it (e.g. a shared child's content).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "recomputeDigest()")]
    void RecomputeDigest()
    {
        GetDigest().Clear();
    }

    /// <summary>
    /// This op's convention.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getConvention()")]
    IConvention Convention
    {
        get { return Traits.Convention; }
    }

    /// <summary>
    /// Whether this op only enforces a physical property on its input (e.g. a converter) rather than
    /// computing a result. The top-down search gives enforcers lower priority. Defaults to <c>false</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "isEnforcer()")]
    bool IsEnforcer => false;

    /// <summary>
    /// This op with any planner wrapping removed — a placeholder op (an equivalence subset, a graph
    /// vertex) returns the concrete op it stands for; an ordinary op returns itself.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "stripped()")]
    IOp Stripped => this;

    /// <summary>
    /// This op's own cost, not counting its inputs. A cost-based planner consults it; a heuristic
    /// planner ignores it. The default is a small positive cost, so an op opts into a real cost model
    /// only by overriding this.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "computeSelfCost(RelOptPlanner, RelMetadataQuery)")]
    IOpCost ComputeSelfCost(IOpPlanner planner)
    {
        return planner.CostFactory.MakeTinyCost();
    }

}
