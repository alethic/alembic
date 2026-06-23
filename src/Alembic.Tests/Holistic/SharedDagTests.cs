using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Hep;

using Alembic.Tests.Languages.Expression;
using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Expression.Rules;

using Xunit;
using Xunit.Abstractions;

namespace Alembic.Tests.Holistic;

public class SharedDagTests
{

    readonly ITestOutputHelper _output;

    public SharedDagTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Common_subexpressions_are_shared_and_rewritten_once()
    {
        var logical = OpTraitSet.CreateEmpty().Plus(ExpressionConventions.Logical);

        var program = HepProgram.Builder()
            .AddRuleInstance(new FoldMultiply())
            .Build();

        var planner = new HepPlanner(program);
        var cluster = new OpCluster(planner);

        // (2 * 3) + (2 * 3): the two multiplies are the same subexpression, so the graph interns them to
        // one vertex. Folding it once rewrites both occurrences, and the result shares one op.
        IOp root = new Add(
            logical,
            new Multiply(logical, new Literal(cluster, logical, 2), new Literal(cluster, logical, 3)),
            new Multiply(logical, new Literal(cluster, logical, 2), new Literal(cluster, logical, 3)));

        planner.SetRoot(root);
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var add = Assert.IsType<Add>(best);
        Assert.Equal(6, Assert.IsType<Literal>(add.Left).Value);
        Assert.Equal(6, Assert.IsType<Literal>(add.Right).Value);

        // The shared subexpression yields one op, not two equal copies — proof the graph is a DAG.
        Assert.Same(add.Left, add.Right);
    }

}
