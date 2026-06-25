using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Hep;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;
using Alembic.Tests.Languages.Relational.Rules;

using Xunit;
using Xunit.Abstractions;

using Alembic.Algebra.Convert;

namespace Alembic.Tests.Holistic;

public class RelationalLoweringTests
{

    readonly ITestOutputHelper _output;

    public RelationalLoweringTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Lowers_filter_over_source()
    {
        var (logical, physical) = Setup();

        var planner = BuildPlanner(Converters(physical));
        var cluster = new OpCluster(planner);
        IOp root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "x > 5");

        var result = Lower(planner, root, physical);

        var filter = Assert.IsType<PhysicalFilter>(result);
        Assert.Equal("x > 5", filter.Predicate);
        Assert.Equal("t", Assert.IsType<PhysicalSource>(filter.Input).Table);
    }

    [Fact]
    public void Pushes_filter_into_source()
    {
        var (logical, physical) = Setup();

        // Lower, then push the physical filter down into the source — a second physical realization.
        var planner = BuildPlanner(new SourceConverter(physical), new FilterConverter(physical), new PushFilterIntoSource());
        var cluster = new OpCluster(planner);
        IOp root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "x > 5");

        var result = Lower(planner, root, physical);

        var scan = Assert.IsType<PhysicalFilteredSource>(result);
        Assert.Equal("t", scan.Table);
        Assert.Equal("x > 5", scan.Predicate);
    }

    [Fact]
    public void Lowers_filter_over_parameter()
    {
        var (logical, physical) = Setup();

        var planner = BuildPlanner(Converters(physical));
        var cluster = new OpCluster(planner);
        IOp root = new LogicalFilter(logical, new LogicalParameter(cluster, logical, "p"), "x > 5");

        var result = Lower(planner, root, physical);

        var filter = Assert.IsType<PhysicalFilter>(result);
        Assert.Equal("p", Assert.IsType<PhysicalParameter>(filter.Input).Name);
    }

    [Fact]
    public void Removes_a_true_filter()
    {
        var (logical, _) = Setup();

        var planner = BuildPlanner(new RemoveTrueFilter());
        var cluster = new OpCluster(planner);
        IOp root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "true");

        var result = Simplify(planner, root);

        Assert.Equal("t", Assert.IsType<LogicalSource>(result).Table);
    }

    [Fact]
    public void Merges_nested_filters()
    {
        var (logical, _) = Setup();

        var planner = BuildPlanner(new MergeFilters());
        var cluster = new OpCluster(planner);
        IOp root = new LogicalFilter(logical, new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "b"), "a");

        var result = Simplify(planner, root);

        var filter = Assert.IsType<LogicalFilter>(result);
        Assert.Equal("a AND b", filter.Predicate);
        Assert.IsType<LogicalSource>(filter.Input);
    }

    [Fact]
    public void Simplifies_then_lowers()
    {
        var (logical, physical) = Setup();

        var planner = BuildPlanner(new RemoveTrueFilter());
        var cluster = new OpCluster(planner);
        IOp root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "true");

        // Phase 1: simplify away the redundant filter in the logical model.
        var simplified = Simplify(planner, root);
        Assert.IsType<LogicalSource>(simplified);

        // Phase 2: lower the residual.
        var lowered = Lower(BuildPlanner(Converters(physical)), simplified, physical);
        Assert.Equal("t", Assert.IsType<PhysicalSource>(lowered).Table);
    }

    [Fact]
    public void Convention_registers_its_lowering_rules()
    {
        // The program applies every converter rule; the physical convention contributes them to the
        // planner, rather than the caller listing them by hand.
        var planner = new HepPlanner(HepProgram.Builder().AddRuleClass<ConverterRule>().Build());
        var cluster = new OpCluster(planner);

        RelationalConventions.Physical.Register(planner);

        var logical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical);
        var physical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Physical);
        IOp root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "x > 5");

        planner.SetRoot(root);
        planner.ChangeTraits(root, physical);
        var result = planner.FindBestPlan();

        var filter = Assert.IsType<PhysicalFilter>(result);
        // The lowered op carries the physical convention (the converters built their target trait set
        // from the planner's empty trait set, which is bare for HEP — see D16).
        Assert.Equal(RelationalConventions.Physical, filter.Convention);
    }

    [Fact]
    public void Incomplete_lowering_returns_a_best_effort_partial_plan()
    {
        var (logical, physical) = Setup();

        // Omit the source converter: the source can never reach the physical convention. Like Calcite's
        // findBestExp, HEP returns the best plan it could reach rather than throwing — the Filter is
        // lowered to physical, but its Source child remains in the logical convention.
        var planner = BuildPlanner(new FilterConverter(physical));
        var cluster = new OpCluster(planner);
        IOp root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "x > 5");

        var best = Lower(planner, root, physical);

        var filter = Assert.IsType<PhysicalFilter>(best);
        Assert.Equal(RelationalConventions.Logical, filter.Inputs[0].Convention);
    }

    static (OpTraitSet Logical, OpTraitSet Physical) Setup()
    {
        return (
            OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical),
            OpTraitSet.CreateEmpty().Plus(RelationalConventions.Physical));
    }

    static OpRule[] Converters(OpTraitSet physical)
    {
        return [new SourceConverter(physical), new FilterConverter(physical), new ParameterConverter(physical)];
    }

    IOp Lower(HepPlanner planner, IOp root, OpTraitSet required)
    {
        planner.SetRoot(root);
        planner.ChangeTraits(root, required);
        var best = planner.FindBestPlan();
        _output.WriteLine("--- lowered ---");
        _output.WriteLine(PlanUtil.ToString(best));
        return best;
    }

    IOp Simplify(HepPlanner planner, IOp root)
    {
        planner.SetRoot(root);
        var best = planner.FindBestPlan();
        _output.WriteLine("--- simplified ---");
        _output.WriteLine(PlanUtil.ToString(best));
        return best;
    }

    static HepPlanner BuildPlanner(params OpRule[] rules)
    {
        // Fire all the rules together to a fixed point (a rule collection), so lowering can cascade
        // across them in one instruction.
        var builder = HepProgram.Builder().AddRuleCollection(rules);
        return new HepPlanner(builder.Build());
    }

}
