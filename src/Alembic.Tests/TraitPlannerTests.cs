using Alembic.Algebra;
using Alembic.Plan.Hep;
using Alembic.Plan;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;

using Xunit;

namespace Alembic.Tests;

public class TraitPlannerTests
{

    [Fact]
    public void A_new_trait_dimension_flows_through_the_planner()
    {
        // Build trait sets carrying convention plus a second dimension (Sortedness), starting at
        // its default (Unsorted).
        var logical = TraitSet.CreateEmpty().Plus(RelationalConventions.Logical).Plus(Sortedness.Unsorted);

        INode root = new LogicalFilter(logical, new LogicalSource(logical, "t"), "x > 5");

        var program = HepProgram.Builder()
            .AddRule(new MarkSorted())
            .Build();

        var planner = new HepPlanner(program);
        planner.SetRoot(root);
        var best = planner.FindBestPlan();

        // The rule read and wrote the new dimension on every node, and the planner preserved both
        // dimensions through Copy. Convention is untouched.
        AssertSorted(best);
    }

    static void AssertSorted(INode node)
    {
        Assert.Equal(Sortedness.Sorted, node.Traits.Get(SortednessTraitDef.Instance));
        Assert.Equal(RelationalConventions.Logical, node.Convention);

        foreach (var child in node.Children)
            AssertSorted(child);
    }

}
