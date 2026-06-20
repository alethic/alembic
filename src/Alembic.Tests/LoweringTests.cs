using Alembic.Algebra;
using Alembic.Plan.Hep;
using Alembic.Plan.Traits;

using Xunit;

namespace Alembic.Tests;

public class LoweringTests
{

    [Fact]
    public void Lowers_logical_tree_to_physical()
    {
        var ctx = new TraitContext();
        var logical = ctx.Empty.Replace(ConventionTraitDef.Instance, Conventions.Logical);
        var physical = ctx.Empty.Replace(ConventionTraitDef.Instance, Conventions.Physical);

        INode root = new LogicalFilter(logical, new LogicalSource(logical, "t"), "x > 5");

        var program = HepProgram.Builder()
            .AddMatchOrder(HepMatchOrder.BottomUp)
            .AddRule(new SourceConverter(physical))
            .AddRule(new FilterConverter(physical))
            .Build();

        var planner = new HepPlanner(program);
        planner.SetRoot(root);
        var best = planner.FindBestPlan();

        var filter = Assert.IsType<PhysicalFilter>(best);
        Assert.Equal("x > 5", filter.Predicate);

        var source = Assert.IsType<PhysicalSource>(filter.Source);
        Assert.Equal("t", source.Table);

        Assert.True(IsFullyConverted(best, Conventions.Physical), "every node should be in the physical convention");
    }

    [Fact]
    public void Interned_trait_sets_are_shared()
    {
        var ctx = new TraitContext();
        var a = ctx.Empty.Replace(ConventionTraitDef.Instance, Conventions.Physical);
        var b = ctx.Empty.Replace(ConventionTraitDef.Instance, Conventions.Physical);

        Assert.Same(a, b);
    }

    static bool IsFullyConverted(INode node, Convention convention)
    {
        if (!node.Convention.Equals(convention))
            return false;

        foreach (var child in node.Children)
        {
            if (!IsFullyConverted(child, convention))
                return false;
        }

        return true;
    }

}
