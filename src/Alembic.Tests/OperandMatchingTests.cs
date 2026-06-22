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
        var logical = TraitSet.CreateEmpty().Plus(RelationalConventions.Logical);

        // An outer filter over an inner filter over a source. Only the inner filter sits directly
        // over a source, so only it should match the Filter(Source) operand.
        INode root = new LogicalFilter(
            logical,
            new LogicalFilter(logical, new LogicalSource(logical, "t"), "inner"),
            "outer");

        var program = HepProgram.Builder()
            .AddRule(new TagFilterOverSource())
            .Build();

        var planner = new HepPlanner(program);
        planner.SetRoot(root);
        var best = Assert.IsType<LogicalFilter>(planner.FindBestPlan());
        _output.WriteLine(best.ToPlanString());

        // Outer filter's child is a filter, not a source — operand does not match, so it is untouched.
        Assert.Equal("outer", best.Predicate);

        // Inner filter is directly over a source — operand matches, so it is tagged.
        var inner = Assert.IsType<LogicalFilter>(best.Input);
        Assert.Equal("tagged", inner.Predicate);
    }

}
