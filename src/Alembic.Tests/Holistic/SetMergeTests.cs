using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Expression;
using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Expression.Rules;

using Xunit;
using Xunit.Abstractions;

namespace Alembic.Tests.Holistic;

public class SetMergeTests
{

    readonly ITestOutputHelper _output;

    public SetMergeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Folding_a_subtree_into_an_existing_equivalent_merges_their_sets()
    {
        var logical = OpTraitSet.CreateEmpty().Plus(ExpressionConventions.Logical);

        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);

        // (2 * 3) + 6 — folding 2*3 to 6 makes the left subtree structurally equal to the right child
        // (which is already its own equivalence set), so the planner must merge the two sets.
        IOp root = new Add(
            logical,
            new Multiply(logical, new Literal(cluster, logical, 2), new Literal(cluster, logical, 3)),
            new Literal(cluster, logical, 6));

        planner.AddRule(new FoldMultiply());
        planner.SetRoot(root);
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var add = Assert.IsType<Add>(best);
        Assert.Equal(6, Assert.IsType<Literal>(add.Left).Value);
        Assert.Equal(6, Assert.IsType<Literal>(add.Right).Value);
    }

}
