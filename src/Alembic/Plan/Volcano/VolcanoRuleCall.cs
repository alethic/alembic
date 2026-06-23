using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The rule call used by the cost-based planner. A match is seeded at one operand (the just-registered
/// node) and solved outward over the equivalence subsets; a rule then registers each equivalent it
/// finds, and the planner keeps them all and later chooses the cheapest.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall")]
public class VolcanoRuleCall : RuleCall
{

    readonly VolcanoPlanner _planner;

    /// <summary>
    /// The nodes bound to each operand, indexed by <see cref="RuleOperand.OrdinalInRule"/>. Filled in as
    /// the match solves outward.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "rels")]
    internal INode?[] Rels { get; }

    /// <summary>
    /// Creates a call seeded at <paramref name="operand0"/>, with no nodes bound yet.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "VolcanoRuleCall(VolcanoPlanner, RelOptRuleOperand)")]
    internal VolcanoRuleCall(VolcanoPlanner planner, RuleOperand operand0)
        : base(planner, operand0, ImmutableArray<INode>.Empty)
    {
        _planner = planner;
        Rels = new INode?[operand0.Rule.Operands.Length];
    }

    /// <summary>
    /// Creates a call over the already-bound nodes (in operand order) of a completed match seeded at
    /// <paramref name="operand0"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "VolcanoRuleCall(VolcanoPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>)")]
    internal VolcanoRuleCall(VolcanoPlanner planner, RuleOperand operand0, ImmutableArray<INode> nodes)
        : base(planner, operand0, nodes)
    {
        _planner = planner;
        Rels = new INode?[operand0.Rule.Operands.Length];
        for (int i = 0; i < nodes.Length; i++)
            Rels[i] = nodes[i];
    }

    /// <summary>
    /// Applies the rule to the bound nodes.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "onMatch()")]
    public virtual void OnMatch()
    {
        if (_planner.IsRuleExcluded(Rule))
            return;

        // Skip the match if any bound node has gone stale since it was queued: its set was merged away, it
        // was removed from its subset (during a rename), or it has been pruned.
        foreach (var rel in Rels)
        {
            if (rel is null)
                continue;

            var subset = _planner.GetSubset(rel);
            if (subset is null)
                return;

            if (subset.Set.EquivalentSet is not null || (!ReferenceEquals(subset, rel) && !subset.Contains(rel)))
                return;

            if (_planner.IsPruned(rel))
                return;
        }

        if (!Rule.Matches(this))
            return;

        _planner.FireRuleAttempted(this, true);
        Rule.OnMatch(this);
        _planner.FireRuleAttempted(this, false);
    }

    /// <summary>
    /// Registers <paramref name="equivalent"/> as another way to compute the matched node, plus any
    /// secondary equivalences in <paramref name="equiv"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "transformTo(RelNode, Map<RelNode, RelNode>, RelHintsPropagator)")]
    public override void TransformTo(INode equivalent, IReadOnlyDictionary<INode, INode> equiv)
    {
        // A transformation rule stays logical; it may not produce a physical node.
        if (equivalent is IPhysicalNode && Rule is ITransformationRule)
            throw new InvalidOperationException($"'{equivalent.GetType().Name}' is a physical node, which the transformation rule '{Rule.Description}' may not produce.");

        // Register the explicit equivalences first, so registering the root below does not register them
        // twice and cause churn.
        foreach (var entry in equiv)
            _planner.EnsureRegistered(entry.Key, entry.Value);

        _planner.EnsureRegistered(equivalent, Node(0));
        _planner.FireRuleProductionSucceeded(this, equivalent);
    }

    /// <summary>
    /// Seeds the match with <paramref name="node"/> in <see cref="RuleCall.Operand0"/>'s slot and solves the rest.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "match(RelNode)")]
    internal void Match(INode node)
    {
        const int solve = 0;
        int operandOrdinal = Operand0.SolveOrder[solve];
        Rels[operandOrdinal] = node;
        MatchRecurse(solve + 1);
    }

    /// <summary>
    /// Recursively matches operands above a given solve order. When all operands are bound, the rule's
    /// side-condition is checked and, if satisfied, <see cref="OnMatch"/> is invoked.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "matchRecurse(int)")]
    void MatchRecurse(int solve)
    {
        var operands = Rule.Operands;
        if (solve == operands.Length)
        {
            if (Rule.Matches(this))
                OnMatch();

            return;
        }

        var solveOrder = Operand0.SolveOrder;
        int operandOrdinal = solveOrder[solve];
        int previousOperandOrdinal = solveOrder[solve - 1];
        bool ascending = operandOrdinal < previousOperandOrdinal;
        var previousOperand = operands[previousOperandOrdinal];
        var operand = operands[operandOrdinal];
        var previous = Rels[previousOperandOrdinal]!;

        RuleOperand parentOperand;
        IEnumerable<INode> successors;
        if (ascending)
        {
            // The operand being solved is an ancestor of the previous one; climb to the previous node's
            // parents.
            parentOperand = operand;
            var subset = _planner.GetSubsetNonNull(previous);
            successors = subset.GetParentRels();
        }
        else
        {
            parentOperand = operand.Parent!;
            var parentRel = Rels[parentOperand.OrdinalInRule]!;
            var inputs = parentRel.Children;
            if (parentOperand.ChildPolicy == RuleOperandChildPolicy.Unordered)
            {
                // An unordered child can bind any input, so all members of all input subsets are
                // candidates.
                successors = AllNodesInInputs(inputs);
            }
            else if (operand.OrdinalInParent < inputs.Length)
            {
                var subset = (NodeSubset)inputs[operand.OrdinalInParent];
                successors = subset.GetRels();
            }
            else
            {
                // The parent does not have the input this operand expects.
                successors = Array.Empty<INode>();
            }
        }

        foreach (var rel in successors)
        {
            // A transformation rule stays within one convention: don't bind a node whose convention
            // differs from the operand we came from.
            if (Rule is ITransformationRule && !rel.Convention.Equals(previous.Convention))
                continue;

            if (!operand.Matches(rel))
                continue;

            if (ascending && operand.ChildPolicy != RuleOperandChildPolicy.Unordered)
            {
                // We know the previous operand matched *a* child of its parent; check that it is the
                // *correct* child of this candidate parent.
                if (previousOperand.OrdinalInParent >= rel.Children.Length)
                    continue;

                var input = (NodeSubset)rel.Children[previousOperand.OrdinalInParent];
                if (!input.Contains(previous))
                    continue;
            }

            Rels[operandOrdinal] = rel;
            MatchRecurse(solve + 1);
        }
    }

    IEnumerable<INode> AllNodesInInputs(ImmutableArray<INode> inputs)
    {
        var seen = new HashSet<INode>(ReferenceEqualityComparer.Instance);
        foreach (var input in inputs)
        {
            if (!seen.Add(input))
                continue;

            foreach (var rel in ((NodeSubset)input).GetRels())
                if (seen.Add(rel))
                    yield return rel;
        }
    }

}
