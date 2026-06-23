using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// A rule match waiting in the <see cref="RuleQueue"/>: a rule plus the ops bound to its operands.
/// Applying it (<see cref="VolcanoRuleCall.OnMatch"/>) registers the rule's equivalents. Two matches
/// are equal when they have the same rule and the same bound ops, so duplicates are dropped.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch")]
internal class VolcanoRuleMatch : VolcanoRuleCall
{

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch", "VolcanoRuleMatch(VolcanoPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>)")]
    internal VolcanoRuleMatch(VolcanoPlanner planner, OpRuleOperand operand0, ImmutableArray<IOp> ops)
        : base(planner, operand0, ops)
    {
        // A completed match must have bound an op to every operand.
        foreach (var op in ops)
            if (op is null)
                throw new ArgumentException("A rule match must have an op bound to every operand.", nameof(ops));
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch", "computeDigest()")]
    public override bool Equals(object? obj)
    {
        if (obj is not VolcanoRuleMatch other || !ReferenceEquals(Rule, other.Rule) || Ops.Length != other.Ops.Length)
            return false;

        for (int i = 0; i < Ops.Length; i++)
            if (!ReferenceEquals(Ops[i], other.Ops[i]))
                return false;

        return true;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch", "computeDigest()")]
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RuntimeHelpers.GetHashCode(Rule));
        foreach (var op in Ops)
            hash.Add(RuntimeHelpers.GetHashCode(op));

        return hash.ToHashCode();
    }

}
