using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The rule call used by the cost-based planner. A rule registers each equivalent it finds; the
/// planner keeps them all and later chooses the cheapest.
/// </summary>
public class VolcanoRuleCall : RuleCall
{

    readonly VolcanoPlanner _planner;

    internal VolcanoRuleCall(VolcanoPlanner planner, IRule rule, ImmutableArray<INode> nodes)
        : base(nodes)
    {
        _planner = planner;
        Rule = rule;
    }

    /// <summary>
    /// The rule this call is for.
    /// </summary>
    public IRule Rule { get; }

    /// <summary>
    /// The planner that issued this call.
    /// </summary>
    internal VolcanoPlanner Planner => _planner;

    /// <summary>
    /// Applies the rule to the bound nodes.
    /// </summary>
    public virtual void OnMatch()
    {
        _planner.FireRuleAttempted(this, true);
        Rule.OnMatch(this);
        _planner.FireRuleAttempted(this, false);
    }

    /// <summary>
    /// Registers <paramref name="equivalent"/> as another way to compute the matched node.
    /// </summary>
    public override void Transform(INode equivalent)
    {
        _planner.Register(equivalent, Node(0));
        _planner.FireRuleProductionSucceeded(this, equivalent);
    }

}
