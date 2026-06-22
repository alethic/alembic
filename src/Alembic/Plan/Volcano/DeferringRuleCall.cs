using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The rule call used while firing rules during registration. Instead of applying the rule
/// immediately, each completed match is deferred by adding a <see cref="VolcanoRuleMatch"/> to the
/// planner's rule queue, for the <see cref="IRuleDriver"/> to apply later.
/// </summary>
public sealed class DeferringRuleCall : VolcanoRuleCall
{

    readonly VolcanoPlanner _planner;

    internal DeferringRuleCall(VolcanoPlanner planner, RuleOperand operand0)
        : base(planner, operand0)
    {
        _planner = planner;
    }

    /// <summary>
    /// Queues the completed match rather than applying it.
    /// </summary>
    public override void OnMatch()
    {
        var builder = ImmutableArray.CreateBuilder<INode>(Rels.Length);
        foreach (var rel in Rels)
            builder.Add(rel!);

        _planner.RuleDriver.Queue.AddMatch(new VolcanoRuleMatch(_planner, Operand0, builder.MoveToImmutable()));
    }

}
