using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Hep;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;

using Xunit;

namespace Alembic.Tests;

/// <summary>
/// Exercises the heuristic planner's instruction model — match orders, match limit, subprograms, and
/// groups — using <see cref="MarkSorted"/>, which tags every node it reaches.
/// </summary>
public class HepProgramTests
{

    static readonly TraitSet Logical = TraitSet.CreateEmpty().Plus(RelationalConventions.Logical).Plus(Sortedness.Unsorted);

    static INode Tree(Cluster cluster) => new LogicalFilter(Logical, new LogicalSource(cluster, Logical, "t"), "x > 5");

    static bool IsSorted(INode node) => ReferenceEquals(node.Traits.Get(SortednessTraitDef.Instance), Sortedness.Sorted);

    static bool AllSorted(INode node)
    {
        if (!IsSorted(node))
            return false;

        foreach (var child in node.Children)
            if (!AllSorted(child))
                return false;

        return true;
    }

    static INode Run(HepProgram program)
    {
        var planner = new HepPlanner(program);
        planner.SetRoot(Tree(new Cluster(planner)));
        return planner.FindBestPlan();
    }

    [Theory]
    [InlineData(HepMatchOrder.TopDown)]
    [InlineData(HepMatchOrder.BottomUp)]
    [InlineData(HepMatchOrder.DepthFirst)]
    [InlineData(HepMatchOrder.Arbitrary)]
    public void Every_match_order_reaches_the_same_fixed_point(HepMatchOrder order)
    {
        var best = Run(HepProgram.Builder().AddMatchOrder(order).AddRuleInstance(new MarkSorted()).Build());

        Assert.True(AllSorted(best));
    }

    [Fact]
    public void A_match_limit_caps_the_number_of_transformations()
    {
        // With a limit of one, only the first node visited (the root, in depth-first order) is rewritten;
        // its input is left untouched.
        var best = Run(HepProgram.Builder().AddMatchLimit(1).AddRuleInstance(new MarkSorted()).Build());

        Assert.True(IsSorted(best));
        Assert.False(IsSorted(best.Children[0]));
    }

    [Fact]
    public void A_subprogram_runs_to_its_own_fixed_point()
    {
        var subprogram = HepProgram.Builder().AddRuleInstance(new MarkSorted()).Build();
        var best = Run(HepProgram.Builder().AddSubprogram(subprogram).Build());

        Assert.True(AllSorted(best));
    }

    [Fact]
    public void A_group_fires_its_collected_rules_together()
    {
        var program = HepProgram.Builder()
            .AddGroupBegin()
            .AddRuleInstance(new MarkSorted())
            .AddGroupEnd()
            .Build();

        Assert.True(AllSorted(Run(program)));
    }

    [Fact]
    public void Large_plan_mode_reaches_the_same_fixed_point()
    {
        // Large-plan mode drives the depth-first pass with HepVertexIterator instead of restarting from
        // the root; the outcome is the same.
        var planner = new HepPlanner(HepProgram.Builder().AddRuleInstance(new MarkSorted()).Build())
        {
            LargePlanMode = true,
        };
        planner.SetRoot(Tree(new Cluster(planner)));

        Assert.True(AllSorted(planner.FindBestPlan()));
    }

    [Fact]
    public void A_rule_class_fires_every_rule_of_that_type()
    {
        var program = HepProgram.Builder().AddRuleClass<MarkSorted>().Build();
        var planner = new HepPlanner(program);
        planner.AddRule(new MarkSorted());
        planner.SetRoot(Tree(new Cluster(planner)));

        Assert.True(AllSorted(planner.FindBestPlan()));
    }

}
