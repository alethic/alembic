using Alembic.Algebra;
using Alembic.Plan.Hep;
using Alembic.Plan;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;

using Xunit;
using Xunit.Abstractions;

namespace Alembic.Tests;

public class TraitPlannerTests
{

    readonly ITestOutputHelper _output;

    public TraitPlannerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void A_new_trait_dimension_flows_through_the_planner()
    {
        // Build trait sets carrying convention plus a second dimension (Sortedness), starting at
        // its default (Unsorted).
        var logical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical).Plus(Sortedness.Unsorted);

        var program = HepProgram.Builder()
            .AddRuleInstance(new MarkSorted())
            .Build();

        var planner = new HepPlanner(program);
        var cluster = new OpCluster(planner);
        IOp root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "x > 5");

        planner.SetRoot(root);
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        // The rule read and wrote the new dimension on every op, and the planner preserved both
        // dimensions through Copy. Convention is untouched.
        AssertSorted(best);
    }

    static void AssertSorted(IOp op)
    {
        Assert.Equal(Sortedness.Sorted, op.Traits.Get(SortednessTraitDef.Instance));
        Assert.Equal(RelationalConventions.Logical, op.Convention);

        foreach (var child in op.Children)
            AssertSorted(child);
    }

}
