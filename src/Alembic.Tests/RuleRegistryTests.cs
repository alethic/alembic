using System.Text.RegularExpressions;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Hep;
using Alembic.Plan.Rules;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;

using Xunit;

namespace Alembic.Tests;

/// <summary>
/// Exercises the planner's rule registry: rules are keyed by a unique description, so a duplicate
/// registration is a no-op, and a description-exclusion filter keeps matching rules from firing.
/// </summary>
public class RuleRegistryTests
{

    static readonly OpTraitSet Logical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical).Plus(Sortedness.Unsorted);

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

    [Fact]
    public void An_excluded_rule_does_not_fire()
    {
        var planner = new HepPlanner(HepProgram.Builder().AddRuleInstance(new MarkSorted()).Build());
        planner.SetRuleDescExclusionFilter(new Regex("MarkSorted"));

        var cluster = new OpCluster(planner);
        planner.SetRoot(new LogicalSource(cluster, Logical, "t"));
        var best = planner.FindBestPlan();

        // MarkSorted would tag the op sorted, but the exclusion filter keeps it from firing.
        Assert.Same(Sortedness.Unsorted, best.Traits.Get(SortednessTraitDef.Instance));
    }

    [Fact]
    public void Clearing_the_exclusion_filter_lets_the_rule_fire_again()
    {
        var planner = new HepPlanner(HepProgram.Builder().AddRuleInstance(new MarkSorted()).Build());
        planner.SetRuleDescExclusionFilter(new Regex("MarkSorted"));
        planner.SetRuleDescExclusionFilter(null);

        var cluster = new OpCluster(planner);
        planner.SetRoot(new LogicalSource(cluster, Logical, "t"));
        var best = planner.FindBestPlan();

        Assert.Same(Sortedness.Sorted, best.Traits.Get(SortednessTraitDef.Instance));
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

}
