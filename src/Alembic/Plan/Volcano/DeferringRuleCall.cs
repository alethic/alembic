using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The rule call used while firing rules during registration. Instead of applying the rule
/// immediately, it defers each match by adding a <see cref="VolcanoRuleMatch"/> to the planner's rule
/// queue, for the <see cref="IRuleDriver"/> to apply later.
/// </summary>
public sealed class DeferringRuleCall : VolcanoRuleCall
{

    internal DeferringRuleCall(VolcanoPlanner planner, IRule rule)
        : base(planner, rule, ImmutableArray<INode>.Empty)
    {

    }

    /// <summary>
    /// Finds every match of the rule's operand rooted at <paramref name="node"/> and queues each one.
    /// </summary>
    public void Match(INode node)
    {
        foreach (var binding in Planner.MatchBindings(Rule.Operand, node))
            Planner.RuleDriver.Queue.AddMatch(new VolcanoRuleMatch(Planner, Rule, binding));
    }

}
