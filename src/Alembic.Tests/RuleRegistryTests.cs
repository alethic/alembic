using System.Text.RegularExpressions;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;

using Xunit;

namespace Alembic.Tests;

/// <summary>
/// Exercises the planner's rule registry: rules are keyed by a unique description, so a duplicate
/// registration is a no-op, and a description-exclusion filter keeps matching rules from firing.
/// </summary>
public class RuleRegistryTests
{

    static readonly OpTraitSet Physical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Physical);

    [Fact]
    public void Adding_a_duplicate_rule_is_a_no_op()
    {
        var planner = new VolcanoPlanner();

        // Two instances are equal (same type, description, and operand), so the second registration is
        // rejected.
        Assert.True(planner.AddRule(new NoOp()));
        Assert.False(planner.AddRule(new NoOp()));

        // Once removed, the description is free again.
        Assert.True(planner.RemoveRule(new NoOp()));
        Assert.True(planner.AddRule(new NoOp()));
    }

    // The rule-exclusion filter is a Volcano feature (Calcite's HEP does not consult it); it is wired
    // into VolcanoRuleCall.OnMatch, so an excluded rule's match is skipped before it can fire.
    [Fact]
    public void An_excluded_rule_does_not_fire()
    {
        var spy = new SpyRule();
        var planner = new VolcanoPlanner();
        planner.AddRule(spy);
        planner.SetRuleDescExclusionFilter(new Regex("SpyRule"));

        var cluster = new OpCluster(planner);
        planner.SetRoot(new PhysicalSource(cluster, Physical, "t"));
        planner.FindBestPlan();

        Assert.False(spy.Fired);
    }

    [Fact]
    public void Clearing_the_exclusion_filter_lets_the_rule_fire_again()
    {
        var spy = new SpyRule();
        var planner = new VolcanoPlanner();
        planner.AddRule(spy);
        planner.SetRuleDescExclusionFilter(new Regex("SpyRule"));
        planner.SetRuleDescExclusionFilter(null);

        var cluster = new OpCluster(planner);
        planner.SetRoot(new PhysicalSource(cluster, Physical, "t"));
        planner.FindBestPlan();

        Assert.True(spy.Fired);
    }

    [Fact]
    public void A_locked_planner_rejects_new_rules()
    {
        var planner = new VolcanoPlanner();
        planner.SetLocked(true);
        Assert.False(planner.AddRule(new NoOp()));

        planner.SetLocked(false);
        Assert.True(planner.AddRule(new NoOp()));
    }

    [Fact]
    public void Clear_resets_the_rule_registry()
    {
        var planner = new VolcanoPlanner();
        Assert.True(planner.AddRule(new NoOp()));

        planner.Clear();

        // After a reset the description is free again, so the same rule can be re-registered.
        Assert.True(planner.AddRule(new NoOp()));
    }

    sealed class NoOp : OpRule
    {

        public NoOp()
            : base(Leaf<LogicalSource>())
        {
        }

        public override void OnMatch(OpRuleCall call)
        {
        }

    }

    // Matches any op and records whether it was ever given the chance to fire.
    sealed class SpyRule : OpRule
    {

        public bool Fired;

        public SpyRule()
            : base(Any<IOp>())
        {
        }

        public override void OnMatch(OpRuleCall call)
        {
            Fired = true;
        }

    }

}
