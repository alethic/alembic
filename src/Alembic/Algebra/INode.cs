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
public interface INode
{

    /// <summary>
    /// The cluster this node belongs to — its shared planning context. Every node in a plan shares one.
    /// </summary>
    Cluster Cluster { get; }

    /// <summary>
    /// The physical properties (convention, etc.) carried by this node.
    /// </summary>
    TraitSet Traits { get; }

    /// <summary>
    /// This node's child nodes, in order.
    /// </summary>
    ImmutableArray<INode> Children { get; }

    /// <summary>
    /// Produces a copy of this node with the given traits and children.
    /// </summary>
    INode Copy(TraitSet traits, ImmutableArray<INode> children);

    /// <summary>
    /// Whether this node is structurally equivalent to <paramref name="other"/>.
    /// </summary>
    bool DeepEquals(INode? other);

    /// <summary>
    /// A hash consistent with <see cref="DeepEquals"/>.
    /// </summary>
    int DeepHashCode();

    /// <summary>
    /// Describes this node to <paramref name="writer"/> — its attributes and inputs. The plan-rendering
    /// and digest machinery drive this. (Calcite's <c>RelNode.explain</c>.)
    /// </summary>
    void Explain(INodeWriter writer);

    /// <summary>
    /// This node's structural digest — the key the planner deduplicates on.
    /// </summary>
    INodeDigest GetDigest()
    {
        return new NodeDigest(this);
    }

    /// <summary>
    /// Recomputes this node's digest, discarding any cached value. A planner calls this after something
    /// a node's digest depends on has changed underneath it (e.g. a shared child's content).
    /// </summary>
    void RecomputeDigest()
    {
        GetDigest().Clear();
    }

    /// <summary>
    /// This node's convention.
    /// </summary>
    IConvention Convention
    {
        get { return Traits.Convention; }
    }

    /// <summary>
    /// Whether this node has no children.
    /// </summary>
    bool IsLeaf
    {
        get { return Children.IsEmpty; }
    }

    /// <summary>
    /// Whether this node only enforces a physical property on its input (e.g. a converter) rather than
    /// computing a result. The top-down search gives enforcers lower priority. Defaults to <c>false</c>.
    /// </summary>
    bool IsEnforcer => false;

    /// <summary>
    /// This node with any planner wrapping removed — a placeholder node (an equivalence subset, a graph
    /// vertex) returns the concrete node it stands for; an ordinary node returns itself.
    /// </summary>
    INode Stripped => this;

    /// <summary>
    /// Produces a copy of this node with a single child replaced.
    /// </summary>
    INode WithChild(int ordinal, INode child)
    {
        return Copy(Traits, Children.SetItem(ordinal, child));
    }

    /// <summary>
    /// This node's own cost, not counting its inputs. A cost-based planner consults it; a heuristic
    /// planner ignores it. The default is a small positive cost, so a node opts into a real cost model
    /// only by overriding this.
    /// </summary>
    ICost ComputeSelfCost(IPlanner planner)
    {
        return planner.CostFactory.MakeTinyCost();
    }

}
