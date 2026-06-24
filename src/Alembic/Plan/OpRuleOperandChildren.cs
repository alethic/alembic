using System.Collections.Generic;
using System.Collections.Immutable;

namespace Alembic.Plan;

/// <summary>
/// Children of an <see cref="OpRuleOperand"/> and the policy for matching them. Often created by
/// <see cref="OpRule.Some"/>, <see cref="OpRule.None"/>, <see cref="OpRule.Any"/>, or
/// <see cref="OpRule.Unordered"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperandChildren")]
public class OpRuleOperandChildren
{

    internal static readonly OpRuleOperandChildren AnyChildren =
        new OpRuleOperandChildren(RuleOperandChildPolicy.Any, ImmutableArray<OpRuleOperand>.Empty);

    internal static readonly OpRuleOperandChildren LeafChildren =
        new OpRuleOperandChildren(RuleOperandChildPolicy.Leaf, ImmutableArray<OpRuleOperand>.Empty);

    internal readonly RuleOperandChildPolicy Policy;
    internal readonly ImmutableArray<OpRuleOperand> Operands;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperandChildren", "RelOptRuleOperandChildren(RelOptRuleOperandChildPolicy, List<RelOptRuleOperand>)")]
    public OpRuleOperandChildren(RuleOperandChildPolicy policy, IReadOnlyList<OpRuleOperand> operands)
    {
        Policy = policy;
        Operands = operands.ToImmutableArray();
    }

}
