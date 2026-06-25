using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Hep;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;

using Xunit;

namespace Alembic.Tests.Plan.Hep;

/// <summary>
/// Exercises the heuristic planner's instruction model — match orders, match limit, subprograms, and
/// groups — using <see cref="MarkSorted"/>, which tags every op it reaches.
/// </summary>
public class HepProgramTests
{

    static readonly OpTraitSet Logical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical).Plus(Sortedness.Unsorted);

    static IOp Tree(OpCluster cluster) => new LogicalFilter(Logical, new LogicalSource(cluster, Logical, "t"), "x > 5");

    static bool IsSorted(IOp op) => ReferenceEquals(op.Traits.Get(SortednessTraitDef.Instance), Sortedness.Sorted);

    static bool AllSorted(IOp op)
    {
        if (!IsSorted(op))
            return false;

        foreach (var child in op.Inputs)
            if (!AllSorted(child))
                return false;

        return true;
    }

    static IOp Run(HepProgram program)
    {
        var planner = new HepPlanner(program);
        planner.SetRoot(Tree(new OpCluster(planner)));
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
        // With a limit of one, only the first op visited (the root, in depth-first order) is rewritten;
        // its input is left untouched.
        var best = Run(HepProgram.Builder().AddMatchLimit(1).AddRuleInstance(new MarkSorted()).Build());

        Assert.True(IsSorted(best));
        Assert.False(IsSorted(best.Inputs[0]));
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
        planner.SetRoot(Tree(new OpCluster(planner)));

        Assert.True(AllSorted(planner.FindBestPlan()));
    }

    [Fact]
    public void A_rule_class_fires_every_rule_of_that_type()
    {
        var program = HepProgram.Builder().AddRuleClass<MarkSorted>().Build();
        var planner = new HepPlanner(program);
        planner.AddRule(new MarkSorted());
        planner.SetRoot(Tree(new OpCluster(planner)));

        Assert.True(AllSorted(planner.FindBestPlan()));
    }

}
