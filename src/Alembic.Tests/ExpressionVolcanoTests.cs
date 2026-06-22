using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Expression;
using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Expression.Physical;

using Xunit;

namespace Alembic.Tests;

public class ExpressionVolcanoTests
{

    [Fact]
    public void Lowers_a_binary_expression_tree_cost_based()
    {
        var logical = TraitSet.CreateEmpty().Plus(ExpressionConventions.Logical);
        var physical = TraitSet.CreateEmpty().Plus(ExpressionConventions.Physical);

        // (a * b) + c — exercises the two-child BiNode shape through the cost-based planner.
        INode root = new Add(logical, new Multiply(logical, new Variable(logical, "a"), new Variable(logical, "b")), new Variable(logical, "c"));

        var planner = new VolcanoPlanner();
        ExpressionConventions.Physical.Register(planner);
        planner.SetRoot(root);
        planner.ChangeTraits(root, physical);
        var best = planner.FindBestPlan();

        var add = Assert.IsType<PhysicalAdd>(best);
        Assert.IsType<PhysicalMultiply>(add.Left);
        Assert.Equal("c", Assert.IsType<PhysicalVariable>(add.Right).Name);
    }

}
