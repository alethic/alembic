using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

using Alembic.Algebra;
using Alembic.Algebra.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The rule call used by the cost-based planner. A match is seeded at one operand (the just-registered
/// op) and solved outward over the equivalence subsets; a rule then registers each equivalent it
/// finds, and the planner keeps them all and later chooses the cheapest.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall")]
public class VolcanoRuleCall : OpRuleCall
{

    readonly VolcanoPlanner _planner;

    /// <summary>
    /// The ops bound to each operand, indexed by <see cref="OpRuleOperand.OrdinalInRule"/>. Filled in as
    /// the match solves outward.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "rels")]
    internal readonly IOp?[] Rels;

    /// <summary>
    /// Creates a call seeded at <paramref name="operand0"/>, with no ops bound yet.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "VolcanoRuleCall(VolcanoPlanner, RelOptRuleOperand)")]
    internal VolcanoRuleCall(VolcanoPlanner planner, OpRuleOperand operand0)
        : base(planner, operand0, ImmutableArray<IOp>.Empty, ImmutableDictionary<IOp, IReadOnlyList<IOp>>.Empty)
    {
        _planner = planner;
        Rels = new IOp?[operand0.Rule.Operands.Length];
    }

    /// <summary>
    /// Creates a call over the already-bound ops (in operand order) of a completed match seeded at
    /// <paramref name="operand0"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "VolcanoRuleCall(VolcanoPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>)")]
    internal VolcanoRuleCall(VolcanoPlanner planner, OpRuleOperand operand0, ImmutableArray<IOp> ops)
        : base(planner, operand0, ops, ImmutableDictionary<IOp, IReadOnlyList<IOp>>.Empty)
    {
        _planner = planner;
        Rels = new IOp?[operand0.Rule.Operands.Length];
        for (int i = 0; i < ops.Length; i++)
            Rels[i] = ops[i];
    }

    /// <summary>
    /// Applies the rule to the bound ops.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "onMatch()")]
    public virtual void OnMatch()
    {
        // The match was already validated in MatchRecurse before being queued; Calcite only asserts it
        // here (it does not re-gate).
        Debug.Assert(Rule.Matches(this), "rule should still match its bound operands");

        // Abort the plan if cancellation (e.g. a timeout) was requested; a rule driver catches this.
        // Checked before the try below so it propagates unwrapped, exactly as Calcite checks cancel
        // before its try/catch.
        _planner.CheckCancel();

        try
        {
            if (_planner.IsRuleExcluded(Rule))
                return;

            // (Calcite also checks isRuleExcluded() for root-node hints, and pushes/pops a ruleCallStack
            // to record each result's rule provenance. Alembic ports neither the hint nor the provenance
            // subsystem.)

            // Skip the match if any bound op has gone stale since it was queued: its set was merged away,
            // it was removed from its subset (during a rename), or it has been pruned.
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

            _planner.FireRuleAttempted(this, true);
            Rule.OnMatch(this);
            _planner.FireRuleAttempted(this, false);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Error while applying rule {Rule}, args [{string.Join(", ", (object?[])Rels)}]", e);
        }
    }

    /// <summary>
    /// Registers <paramref name="equivalent"/> as another way to compute the matched op, plus any
    /// secondary equivalences in <paramref name="equiv"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "transformTo(RelNode, Map<RelNode, RelNode>, RelHintsPropagator)")]
    public override void TransformTo(IOp equivalent, IReadOnlyDictionary<IOp, IOp> equiv)
    {
        // A transformation rule stays logical; it may not produce a physical op.
        if (equivalent is IPhysicalOp && Rule is ITransformationRule)
            throw new InvalidOperationException($"'{equivalent.GetType().Name}' is a physical op, which the transformation rule '{Rule.Description}' may not produce.");

        _planner.FireRuleProductionSucceeded(this, equivalent, before: true);

        // A substitution rule that opts in prunes the original op, since its substitute supersedes it.
        if (Rule is ISubstitutionRule substitution && substitution.AutoPruneOld)
            _planner.Prune(Op(0));

        // Register the explicit equivalences first, so registering the root below does not register them
        // twice and cause churn.
        foreach (var entry in equiv)
            _planner.EnsureRegistered(entry.Key, entry.Value);

        _planner.EnsureRegistered(equivalent, Op(0));

        _planner.FireRuleProductionSucceeded(this, equivalent, before: false);
    }

    /// <summary>
    /// Seeds the match with <paramref name="op"/> in <see cref="OpRuleCall.Operand0"/>'s slot and solves the rest.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "match(RelNode)")]
    internal void Match(IOp op)
    {
        const int solve = 0;
        int operandOrdinal = Operand0.SolveOrder[solve];
        Rels[operandOrdinal] = op;
        MatchRecurse(solve + 1);
    }

    /// <summary>
    /// Recursively matches operands above a given solve order. When all operands are bound, the rule's
    /// side-condition is checked and, if satisfied, <see cref="OnMatch"/> is invoked.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleCall", "matchRecurse(int)")]
    void MatchRecurse(int solve)
    {
        Debug.Assert(solve > 0);
        Debug.Assert(solve <= Rule.Operands.Length);

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

        OpRuleOperand parentOperand;
        IEnumerable<IOp> successors;
        if (ascending)
        {
            // The operand being solved is an ancestor of the previous one; climb to the previous op's
            // parents.
            Debug.Assert(ReferenceEquals(previousOperand.Parent, operand));
            Debug.Assert(operand.MatchedType != typeof(OpSubset));

            if (previousOperand.MatchedType != typeof(OpSubset) && previous is OpSubset)
                throw new InvalidOperationException($"OpSubset should not match with {previousOperand.MatchedType.Name}");

            parentOperand = operand;
            var subset = _planner.GetSubsetNonNull(previous);
            successors = subset.GetParentRels();
        }
        else
        {
            parentOperand = operand.Parent ?? throw new NullReferenceException($"operand.Parent for {operand}");
            var parentRel = Rels[parentOperand.OrdinalInRule]!;
            var inputs = parentRel.Children;
            // If the child is unordered, then add all ops in all input subsets to the successors list
            // because unordered can match a child in any ordinal.
            if (parentOperand.ChildPolicy == RuleOperandChildPolicy.Unordered)
            {
                if (operand.MatchedType == typeof(OpSubset))
                {
                    // Find all the sibling subsets that satisfy this subset's trait set.
                    var subsets = new List<IOp>();
                    foreach (var input in inputs)
                        foreach (var subset in ((OpSubset)input).GetSubsetsSatisfyingThis())
                            subsets.Add(subset);
                    successors = subsets;
                }
                else
                {
                    var allOpsInAllSubsets = new List<IOp>();
                    var duplicates = new HashSet<IOp>(ReferenceEqualityComparer.Instance);
                    foreach (var input in inputs)
                    {
                        if (!duplicates.Add(input))
                            // Ignore duplicate subsets.
                            continue;

                        var inputSubset = (OpSubset)input;
                        foreach (var rel in inputSubset.GetRels())
                        {
                            if (!duplicates.Add(rel))
                                // Ignore duplicate ops.
                                continue;

                            allOpsInAllSubsets.Add(rel);
                        }
                    }

                    successors = allOpsInAllSubsets;
                }
            }
            else if (operand.OrdinalInParent < inputs.Length)
            {
                var subset = (OpSubset)inputs[operand.OrdinalInParent];
                if (operand.MatchedType == typeof(OpSubset))
                    // The operand matches subsets directly: bind the sibling subsets that satisfy this
                    // subset's trait set.
                    successors = subset.GetSubsetsSatisfyingThis();
                else
                    successors = subset.GetRels();
            }
            else
            {
                // The parent does not have the input this operand expects.
                successors = Array.Empty<IOp>();
            }
        }

        foreach (var rel in successors)
        {
            // A transformation rule stays within one convention: don't bind an op whose convention
            // differs from the operand we came from.
            if (Rule is ITransformationRule && !ReferenceEquals(rel.Convention, previous.Convention))
                continue;

            if (!operand.Matches(rel))
                continue;

            if (ascending && operand.ChildPolicy != RuleOperandChildPolicy.Unordered)
            {
                // We know the previous operand matched *a* child of its parent; check that it is the
                // *correct* child of this candidate parent.
                if (previousOperand.OrdinalInParent >= rel.Children.Length)
                    continue;

                var input = (OpSubset)rel.Children[previousOperand.OrdinalInParent];
                if (previousOperand.MatchedType == typeof(OpSubset))
                {
                    // The matched subset (previous) must satisfy our input subset.
                    bool found = false;
                    foreach (var sub in input.GetSubsetsSatisfyingThis())
                    {
                        if (sub.Equals(previous))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        continue;
                }
                else if (!input.Contains(previous))
                {
                    continue;
                }
            }

            // Assign "childRels" if the operand is UNORDERED.
            if (parentOperand.ChildPolicy == RuleOperandChildPolicy.Unordered)
            {
                // Note: this is ill-defined. Suppose there's a union with 3 inputs, and the rule is
                // written as Union.class, unordered(...). What should be provided for the other 2
                // arguments? Subsets? Random relations from those subsets? For now, no Alembic-bundled
                // rule reads getChildRels (it is public API for downstream rules), so the bug just waits.
                if (ascending)
                {
                    var inputs = new List<IOp>(rel.Children);
                    inputs[previousOperand.OrdinalInParent] = previous;
                    SetChildRels(rel, inputs);
                }
                else
                {
                    var existing = GetChildRels(previous);
                    var inputs = existing is not null ? new List<IOp>(existing) : new List<IOp>(previous.Children);
                    inputs[operand.OrdinalInParent] = rel;
                    SetChildRels(previous, inputs);
                }
            }

            Rels[operandOrdinal] = rel;
            MatchRecurse(solve + 1);
        }
    }

}
