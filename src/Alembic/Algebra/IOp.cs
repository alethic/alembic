using System.Collections.Immutable;

using Alembic.Algebra.Metadata;
using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// An op in a plan. Alembic attaches no meaning to an op; the user supplies the
/// concrete types and the rules that rewrite them.
/// </summary>
/// <remarks>
/// The planner rewrites either by replacing an op's inputs in place (<see cref="ReplaceInput"/>, as
/// Calcite's planner does during registration) or by producing new ops via <see cref="Copy"/>. An op's
/// own <c>Equals</c>/<c>GetHashCode</c> are left as reference identity; structural equivalence — the
/// value the planner deduplicates on — is the separate <see cref="DeepEquals"/> / <see cref="DeepHashCode"/>
/// contract.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode")]
public interface IOp
{

    /// <summary>
    /// This op's unique, stable id, assigned in creation order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptNode", "getId()")]
    int Id { get; }

    /// <summary>
    /// The cluster this op belongs to — its shared planning context. Every op in a plan shares one.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptNode", "getCluster()")]
    OpCluster Cluster { get; }

    /// <summary>
    /// The physical properties (convention, etc.) carried by this op.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptNode", "getTraitSet()")]
    OpTraitSet Traits { get; }

    /// <summary>
    /// This op's child ops, in order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptNode", "getInputs()")]
    ImmutableArray<IOp> Children { get; }

    /// <summary>
    /// Produces a copy of this op with the given traits and children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "copy(RelTraitSet, List<RelNode>)")]
    IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children);

    /// <summary>
    /// Replaces this op's <paramref name="ordinalInParent"/>th input with <paramref name="p"/>, in place.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "replaceInput(int, RelNode)")]
    void ReplaceInput(int ordinalInParent, IOp p);

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
    /// Visits each of this op's children in order with <paramref name="visitor"/> — the double dispatch
    /// that <see cref="OpVisitor"/> descends through.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "childrenAccept(RelVisitor)")]
    void ChildrenAccept(OpVisitor visitor);

    /// <summary>
    /// This op's structural digest — the key the planner deduplicates on.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getRelDigest()")]
    IOpDigest GetOpDigest();

    /// <summary>
    /// This op's digest in string form.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getDigest()")]
    string GetDigest();

    /// <summary>
    /// Recomputes this op's digest, discarding any cached value. A planner calls this after something
    /// an op's digest depends on has changed underneath it (e.g. a shared child's content).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "recomputeDigest()")]
    void RecomputeDigest();

    /// <summary>
    /// Registers this op's inputs with <paramref name="planner"/> and returns the op to register in its
    /// place: each input replaced by its registered form, as a <see cref="Copy"/> if any changed, with the
    /// digest recomputed. (Unlike Calcite, Alembic does no convention coercion here — see
    /// <c>VolcanoPlanner.CoerceInputConventions</c>.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "onRegister(RelOptPlanner)")]
    IOp OnRegister(IOpPlanner planner);

    /// <summary>
    /// Registers any rules specific to this kind of op. The planner calls this the first time it sees an
    /// op of this class; the default does nothing.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "register(RelOptPlanner)")]
    void Register(IOpPlanner planner);

    /// <summary>
    /// This op's convention, or <c>null</c> if it carries no convention dimension. (Calcite's
    /// <c>getConvention()</c> is abstract and <c>@Nullable</c>; the abstract base derives it from the
    /// trait set.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "getConvention()")]
    IConvention? Convention { get; }

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
    /// planner ignores it. (Calcite leaves this abstract on the interface; the abstract base supplies the
    /// default.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "computeSelfCost(RelOptPlanner, RelMetadataQuery)")]
    IOpCost? ComputeSelfCost(IOpPlanner planner, OpMetadataQuery mq);

    /// <summary>
    /// Whether this op is valid, reporting any failure through <paramref name="litmus"/> (which may throw
    /// or return false). The base op is always valid; an op may override to check its own invariants.
    /// (Calcite's second <c>Context</c> parameter is correlation-only — relational — so it is dropped.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "isValid(Litmus, Context)")]
    bool IsValid(Alembic.Util.Litmus litmus);

}
