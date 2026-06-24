using System;
using System.Collections.Immutable;
using System.Text;

using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// A rule match waiting in the <see cref="RuleQueue"/>: a rule plus the ops bound to its operands.
/// Applying it (<see cref="VolcanoRuleCall.OnMatch"/>) registers the rule's equivalents. A match's
/// <see cref="ToString"/> is its digest — the rule plus its bound ops' ids — by which the queue drops
/// duplicates.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch")]
internal class VolcanoRuleMatch : VolcanoRuleCall
{

    string _digest;

    /// <summary>
    /// Creates a completed match binding <paramref name="ops"/> to the operands rooted at
    /// <paramref name="operand0"/>; throws if any operand is unbound.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch", "VolcanoRuleMatch(VolcanoPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>)")]
    internal VolcanoRuleMatch(VolcanoPlanner planner, OpRuleOperand operand0, ImmutableArray<IOp> ops)
        : base(planner, operand0, ops)
    {
        // A completed match must have bound an op to every operand.
        foreach (var op in ops)
            if (op is null)
                throw new ArgumentException("A rule match must have an op bound to every operand.", nameof(ops));

        _digest = ComputeDigest();
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch", "toString()")]
    public override string ToString() => _digest;

    /// <summary>
    /// Recomputes this match's digest (after its bound ops have changed).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch", "recomputeDigest()")]
    public void RecomputeDigest() => _digest = ComputeDigest();

    /// <summary>
    /// The digest by which two matches are deemed equivalent: the rule and the ids of its bound ops.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch", "computeDigest()")]
    string ComputeDigest()
    {
        var buf = new StringBuilder("rule [" + Rule.Description + "] rels [");
        for (int i = 0; i < Rels.Length; i++)
        {
            if (i > 0)
                buf.Append(',');

            buf.Append('#').Append(Rels[i]!.Id);
        }

        buf.Append(']');
        return buf.ToString();
    }

}
