using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// A rule match waiting in the <see cref="RuleQueue"/>: a rule plus the nodes bound to its operands.
/// Applying it (<see cref="VolcanoRuleCall.OnMatch"/>) registers the rule's equivalents. Two matches
/// are equal when they have the same rule and the same bound nodes, so duplicates are dropped.
/// </summary>
[Provenance("org.apache.calcite.plan.volcano.VolcanoRuleMatch")]
public sealed class VolcanoRuleMatch : VolcanoRuleCall
{

    [Provenance("org.apache.calcite.plan.volcano.VolcanoRuleMatch", "VolcanoRuleMatch(VolcanoPlanner, RelOptRuleOperand, RelNode[], Map<RelNode, List<RelNode>>)")]
    internal VolcanoRuleMatch(VolcanoPlanner planner, RuleOperand operand0, ImmutableArray<INode> nodes)
        : base(planner, operand0, nodes)
    {
        // A completed match must have bound a node to every operand.
        foreach (var node in nodes)
            if (node is null)
                throw new ArgumentException("A rule match must have a node bound to every operand.", nameof(nodes));
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoRuleMatch", "computeDigest()")]
    public override bool Equals(object? obj)
    {
        if (obj is not VolcanoRuleMatch other || !ReferenceEquals(Rule, other.Rule) || Nodes.Length != other.Nodes.Length)
            return false;

        for (int i = 0; i < Nodes.Length; i++)
            if (!ReferenceEquals(Nodes[i], other.Nodes[i]))
                return false;

        return true;
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoRuleMatch", "computeDigest()")]
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RuntimeHelpers.GetHashCode(Rule));
        foreach (var node in Nodes)
            hash.Add(RuntimeHelpers.GetHashCode(node));

        return hash.ToHashCode();
    }

}
