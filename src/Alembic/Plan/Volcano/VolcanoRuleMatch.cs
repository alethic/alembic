using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// A rule match waiting in the <see cref="RuleQueue"/>: a rule plus the ops bound to its operands.
/// Applying it (<see cref="VolcanoRuleCall.OnMatch"/>) registers the rule's equivalents. Two matches are
/// duplicates when they fire the same rule on the same bound ops; the queue drops those via
/// <see cref="Comparer"/>. A match's <see cref="ToString"/> renders that same identity as a digest string
/// for display.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch")]
internal class VolcanoRuleMatch : VolcanoRuleCall
{

    // Built lazily, only when the match is rendered (e.g. tracing). Calcite computes this eagerly in the
    // constructor and deduplicates on a Set<String> of it; Alembic deduplicates structurally via
    // Comparer, so the string — a StringBuilder + String + char[] per match — is never built on the hot
    // path.
    string? _digest;

    /// <summary>
    /// Creates a completed match binding <paramref name="rels"/> to the operands rooted at
    /// <paramref name="operand0"/>; throws if any operand is unbound.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch", "VolcanoRuleMatch(VolcanoPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>)")]
    internal VolcanoRuleMatch(VolcanoPlanner planner, OpRuleOperand operand0, IOp?[] rels, IDictionary<IOp, IReadOnlyList<IOp>> nodeInputs)
        : base(planner, operand0, (IOp?[])rels.Clone(), nodeInputs)
    {
        // A completed match must have bound an op to every operand.
        Debug.Assert(rels.All(op => op is not null));
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch", "toString()")]
    public override string ToString() => _digest ??= ComputeDigest();

    /// <summary>
    /// Recomputes this match's digest (after its bound ops have changed).
    /// </summary>
    [Obsolete("To be removed before 2.0")]
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch", "recomputeDigest()")]
    public void RecomputeDigest() => _digest = ComputeDigest();

    /// <summary>
    /// The digest by which two matches are deemed equivalent: the rule and the ids of its bound ops.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoRuleMatch", "computeDigest()")]
    string ComputeDigest()
    {
        var buf = new StringBuilder("rule [" + Rule.Description + "] rels [");
        for (int i = 0; i < Ops.Length; i++)
        {
            if (i > 0)
                buf.Append(',');

            buf.Append('#').Append(Ops[i]!.Id);
        }

        buf.Append(']');
        return buf.ToString();
    }

    /// <summary>
    /// Deduplicates matches structurally: two matches are equal when they fire the same rule (a planner
    /// requires unique rule descriptions, so <see cref="OpRule"/> identity mirrors the digest's rule name)
    /// on the same bound ops, compared by id. This is exactly what <see cref="ComputeDigest"/>'s string
    /// encoded — the queues use it instead of a <c>HashSet&lt;string&gt;</c> so no digest string is built
    /// per match. (Alembic optimization; Calcite keys its <c>matchList.names</c> set on the string.)
    /// </summary>
    internal sealed class Comparer : IEqualityComparer<VolcanoRuleMatch>
    {

        internal static readonly Comparer Instance = new Comparer();

        public bool Equals(VolcanoRuleMatch? x, VolcanoRuleMatch? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x is null || y is null || !x.Rule.Equals(y.Rule))
                return false;

            var xo = x.Ops;
            var yo = y.Ops;
            if (xo.Length != yo.Length)
                return false;

            for (int i = 0; i < xo.Length; i++)
                if (xo[i]!.Id != yo[i]!.Id)
                    return false;

            return true;
        }

        public int GetHashCode(VolcanoRuleMatch match)
        {
            var hash = new HashCode();
            hash.Add(match.Rule);
            var ops = match.Ops;
            for (int i = 0; i < ops.Length; i++)
                hash.Add(ops[i]!.Id);

            return hash.ToHashCode();
        }

    }

}
