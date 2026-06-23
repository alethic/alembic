using Alembic.Algebra;
using Alembic.Plan.Hep;
using Alembic.Plan;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;

using Xunit;
using Xunit.Abstractions;

namespace Alembic.Tests;

public class OperandMatchingTests
{

    readonly ITestOutputHelper _output;

    public OperandMatchingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Operand_rule_matches_only_the_nested_pattern()
    {
        var logical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical);

        var program = HepProgram.Builder()
            .AddRuleInstance(new TagFilterOverSource())
            .Build();

        var planner = new HepPlanner(program);
        var cluster = new OpCluster(planner);

        // An outer filter over an inner filter over a source. Only the inner filter sits directly
        // over a source, so only it should match the Filter(Source) operand.
        IOpNode root = new LogicalFilter(
            logical,
            new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "inner"),
            "outer");

        planner.SetRoot(root);
        var best = Assert.IsType<LogicalFilter>(planner.FindBestPlan());
        _output.WriteLine(PlanUtil.ToString(best));

        // Outer filter's child is a filter, not a source — operand does not match, so it is untouched.
        Assert.Equal("outer", best.Predicate);

        // Inner filter is directly over a source — operand matches, so it is tagged.
        var inner = Assert.IsType<LogicalFilter>(best.Input);
        Assert.Equal("tagged", inner.Predicate);
    }

}
