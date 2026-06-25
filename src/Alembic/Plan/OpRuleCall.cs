using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using Alembic.Algebra;
using Alembic.Algebra.Metadata;

namespace Alembic.Plan;

/// <summary>
/// The context of a single rule match: the ops bound to each operand and <see cref="TransformTo(IOp)"/> —
/// the sink through which the rule registers an equivalent. This base is planner-agnostic; each
/// planner provides a subclass that decides what <see cref="TransformTo(IOp)"/> does.
/// </summary>
/// <remarks>
/// A rule reaches its matched ops through <see cref="Op"/>, not by navigating
/// <see cref="IOp.Children"/>: under a heuristic planner the children are the concrete ops, but
/// under a cost-based planner they are equivalence subsets, so only the operand-bound ops are
/// guaranteed to be the concrete types the rule expects.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall")]
public abstract class OpRuleCall
{

    static int _nextId;

    /// <summary>
    /// Creates a call seeded at <paramref name="operand0"/>, over the ops bound to the rule's operands
    /// in operand order (the operand root first, then a pre-order walk of the child operands). The rule
    /// is taken from the seed operand.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "RelOptRuleCall(RelOptPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>, List<RelNode>)")]
    protected OpRuleCall(IOpPlanner planner, OpRuleOperand operand0, IOp?[] rels, IDictionary<IOp, IReadOnlyList<IOp>> nodeInputs, IReadOnlyList<IOp>? parents)
    {
        Id = _nextId++;
        Planner = planner;
        Operand0 = operand0;
        Rule = operand0.Rule;
        Ops = rels;
        _nodeInputs = nodeInputs;
        Parents = parents;
        Debug.Assert(rels.Length == Rule.Operands.Length);
    }

    /// <summary>
    /// Creates a call with no recorded parents (they default to <c>null</c>).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "RelOptRuleCall(RelOptPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>)")]
    protected OpRuleCall(IOpPlanner planner, OpRuleOperand operand0, IOp?[] rels, IDictionary<IOp, IReadOnlyList<IOp>> nodeInputs)
        : this(planner, operand0, rels, nodeInputs, null)
    {
    }

    // For each node matched with matchAnyChildren=true (an UNORDERED operand), the node's inputs as
    // seen by this call. This is the only way a rule can retrieve those children, since an unordered
    // operand does not bind them to numbered operands. Reassigned by SetChildRels on first write (the
    // ctor may have been handed an immutable empty map).
    IDictionary<IOp, IReadOnlyList<IOp>> _nodeInputs;

    /// <summary>
    /// The recorded inputs of each op matched by an unordered operand, keyed by op. Subclasses read the
    /// whole map to pass it on when building a derived call or match.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "nodeInputs")]
    protected IDictionary<IOp, IReadOnlyList<IOp>> NodeInputs => _nodeInputs;

    /// <summary>
    /// This call's stable identity, assigned in creation order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "id")]
    public readonly int Id;

    /// <summary>
    /// The planner that issued this call.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "getPlanner()")]
    public IOpPlanner Planner { get; }

    /// <summary>
    /// The operand the match is seeded from (the operand bound to the op that started the match). The
    /// rule is reached through it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "getOperand0()")]
    public OpRuleOperand Operand0 { get; }

    /// <summary>
    /// The rule this call is for.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "getRule()")]
    public OpRule Rule { get; }

    /// <summary>
    /// The ops bound to the rule's operands, indexed by <see cref="OpRuleOperand.OrdinalInRule"/>. The
    /// contents are mutable: slots are filled in as a match solves outward, and hold the bound ops once
    /// the match is complete.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "rels")]
    public readonly IOp?[] Ops;

    /// <summary>
    /// The parents of the first matched op — the common-subexpression context for a
    /// <see cref="ICommonSubExprRule"/>; <c>null</c> otherwise.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "getParents()")]
    public IReadOnlyList<IOp>? Parents { get; }

    /// <summary>
    /// The op bound to the operand at the given ordinal. The operand root is ordinal 0.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "rel(int)")]
    public IOp Op(int ordinal) => Ops[ordinal]!;

    /// <summary>
    /// A snapshot of the ops bound to the rule's operands, in operand order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "getRelList()")]
    public ImmutableArray<IOp> GetOpList() => ImmutableArray.CreateRange(Ops.Select(r => r!));

    /// <summary>
    /// Whether the rule is excluded from firing on this match. Calcite returns <c>true</c> if any bound op
    /// is <c>Hintable</c> and its cluster's hint strategies exclude the rule; Alembic ports no hints, so no
    /// op is <c>Hintable</c> and the loop always falls through — the result is always <c>false</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "isRuleExcluded()")]
    public bool IsRuleExcluded() => false;

    /// <summary>
    /// The metadata query for this call — the one on the matched op's cluster.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "getMetadataQuery()")]
    public OpMetadataQuery GetMetadataQuery() => Op(0).Cluster.GetMetadataQuery();

    /// <summary>
    /// The inputs of an op matched by an operand whose child policy is <see cref="RuleOperandChildPolicy.Unordered"/>,
    /// or <c>null</c> for any other op. An unordered operand does not bind a node's children to numbered
    /// operands, so this is the only way a rule can reach them.
    /// </summary>
    /// <remarks>
    /// Produces a wrong result for the <c>Unordered</c> case under the cost-based planner (the inputs
    /// are equivalence subsets, not the matched ops); the heuristic planner records the concrete inputs.
    /// </remarks>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "getChildRels(RelNode)")]
    public IReadOnlyList<IOp>? GetChildOps(IOp op) => _nodeInputs.TryGetValue(op, out var inputs) ? inputs : null;

    /// <summary>
    /// Records the inputs of <paramref name="op"/> as seen by this call. Only called for an operand
    /// whose child policy is any/unordered.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "setChildRels(RelNode, List<RelNode>)")]
    protected void SetChildRels(IOp op, IReadOnlyList<IOp> inputs)
    {
        if (_nodeInputs.Count == 0)
            _nodeInputs = new Dictionary<IOp, IReadOnlyList<IOp>>();

        _nodeInputs[op] = inputs;
    }

    /// <summary>
    /// Registers an equivalent for the matched op, with no other equivalences.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "transformTo(RelNode)")]
    public void TransformTo(IOp equivalent) => TransformTo(equivalent, ImmutableDictionary<IOp, IOp>.Empty);

    /// <summary>
    /// Registers an equivalent for the matched op, along with a map of other equivalences to register
    /// first (each key is registered as equivalent to its value, so the root registration below does not
    /// register them twice). What this does is planner-specific.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleCall", "transformTo(RelNode, Map<RelNode, RelNode>)")]
    public abstract void TransformTo(IOp equivalent, IReadOnlyDictionary<IOp, IOp> equiv);

}
