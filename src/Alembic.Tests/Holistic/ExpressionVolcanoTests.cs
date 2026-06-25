using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Expression;
using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Expression.Physical;

using Xunit;
using Xunit.Abstractions;

namespace Alembic.Tests.Holistic;

public class ExpressionVolcanoTests
{

    readonly ITestOutputHelper _output;

    public ExpressionVolcanoTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Lowers_a_binary_expression_tree_cost_based()
    {
        var logical = OpTraitSet.CreateEmpty().Plus(ExpressionConventions.Logical);
        var physical = OpTraitSet.CreateEmpty().Plus(ExpressionConventions.Physical);

        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);

        // (a * b) + c — exercises the two-child op shape through the cost-based planner.
        IOp root = new Add(logical, new Multiply(logical, new Variable(cluster, logical, "a"), new Variable(cluster, logical, "b")), new Variable(cluster, logical, "c"));

        ExpressionConventions.Physical.Register(planner);
        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, physical));
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var add = Assert.IsType<PhysicalAdd>(best);
        Assert.IsType<PhysicalMultiply>(add.Left);
        Assert.Equal("c", Assert.IsType<PhysicalVariable>(add.Right).Name);
    }

}
