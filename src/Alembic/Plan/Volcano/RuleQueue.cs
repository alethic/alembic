using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// Holds the rule matches the planner has discovered but not yet applied. Each search strategy supplies
/// its own queue: the bottom-up search drains a FIFO (<see cref="IterativeRuleQueue"/>), the top-down
/// search pulls matches per op and in a rule-priority order (<see cref="TopDownRuleQueue"/>).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleQueue")]
public abstract class RuleQueue
{

    /// <summary>
    /// The planner whose matches this queue holds.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleQueue", "RuleQueue(VolcanoPlanner)")]
    protected RuleQueue(VolcanoPlanner planner)
    {
        Planner = planner;
    }

    /// <summary>
    /// The planner whose matches this queue holds.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleQueue", "planner")]
    protected readonly VolcanoPlanner Planner;

    /// <summary>
    /// Adds a match to the queue.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleQueue", "addMatch(VolcanoRuleMatch)")]
    internal abstract void AddMatch(VolcanoRuleMatch match);

    /// <summary>
    /// Empties the queue, returning whether it held any matches beforehand.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleQueue", "clear()")]
    public abstract bool Clear();

    /// <summary>
    /// Whether a queued match should be skipped: when any of its bound ops has been pruned, or when the
    /// same subset appears more than once along a path from the root operand to a leaf (a cycle — an op
    /// consuming its own output, which would only generate useless equivalents).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleQueue", "skipMatch(VolcanoRuleMatch)")]
    private protected virtual bool SkipMatch(VolcanoRuleMatch match)
    {
        foreach (var rel in match.Ops)
            if (Planner.IsPruned(rel))
                return true;

        return HasDuplicateSubsetOnPath(new Stack<OpSubset>(), match.Rule.Operand, match.Ops);
    }

    /// <summary>
    /// Whether the subset bound to <paramref name="operand"/> already appears on the current root-to-leaf
    /// path (held in <paramref name="subsets"/>); recurses into the operand's children. Duplicate subsets
    /// on different paths are fine — only a repeat along one path is a cycle.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RuleQueue", "checkDuplicateSubsets(Deque<RelSubset>, RelOptRuleOperand, RelNode[])")]
    bool HasDuplicateSubsetOnPath(Stack<OpSubset> subsets, OpRuleOperand operand, ImmutableArray<IOp> rels)
    {
        var subset = Planner.GetSubsetNonNull(rels[operand.OrdinalInRule]);
        if (subsets.Contains(subset))
            return true;

        if (operand.Children.Length > 0)
        {
            subsets.Push(subset);
            foreach (var child in operand.Children)
                if (HasDuplicateSubsetOnPath(subsets, child, rels))
                    return true;

            subsets.Pop();
        }

        return false;
    }

}
