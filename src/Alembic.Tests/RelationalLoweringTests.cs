using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Hep;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;
using Alembic.Tests.Languages.Relational.Rules;

using Xunit;

namespace Alembic.Tests;

public class RelationalLoweringTests
{

    [Fact]
    public void Lowers_filter_over_source()
    {
        var (logical, physical) = Setup();

        INode root = new LogicalFilter(logical, new LogicalSource(logical, "t"), "x > 5");

        var result = Lower(root, physical, Converters(physical));

        var filter = Assert.IsType<PhysicalFilter>(result);
        Assert.Equal("x > 5", filter.Predicate);
        Assert.Equal("t", Assert.IsType<PhysicalSource>(filter.Input).Table);
    }

    [Fact]
    public void Pushes_filter_into_source()
    {
        var (logical, physical) = Setup();

        INode root = new LogicalFilter(logical, new LogicalSource(logical, "t"), "x > 5");

        // Lower, then push the physical filter down into the source — a second physical realization.
        var result = Lower(root, physical, new SourceConverter(physical), new FilterConverter(physical), new PushFilterIntoSource());

        var scan = Assert.IsType<PhysicalFilteredSource>(result);
        Assert.Equal("t", scan.Table);
        Assert.Equal("x > 5", scan.Predicate);
    }

    [Fact]
    public void Lowers_filter_over_parameter()
    {
        var (logical, physical) = Setup();

        INode root = new LogicalFilter(logical, new LogicalParameter(logical, "p"), "x > 5");

        var result = Lower(root, physical, Converters(physical));

        var filter = Assert.IsType<PhysicalFilter>(result);
        Assert.Equal("p", Assert.IsType<PhysicalParameter>(filter.Input).Name);
    }

    [Fact]
    public void Removes_a_true_filter()
    {
        var (logical, _) = Setup();

        INode root = new LogicalFilter(logical, new LogicalSource(logical, "t"), "true");

        var result = Simplify(root, new RemoveTrueFilter());

        Assert.Equal("t", Assert.IsType<LogicalSource>(result).Table);
    }

    [Fact]
    public void Merges_nested_filters()
    {
        var (logical, _) = Setup();

        INode root = new LogicalFilter(logical, new LogicalFilter(logical, new LogicalSource(logical, "t"), "b"), "a");

        var result = Simplify(root, new MergeFilters());

        var filter = Assert.IsType<LogicalFilter>(result);
        Assert.Equal("a AND b", filter.Predicate);
        Assert.IsType<LogicalSource>(filter.Input);
    }

    [Fact]
    public void Simplifies_then_lowers()
    {
        var (logical, physical) = Setup();

        INode root = new LogicalFilter(logical, new LogicalSource(logical, "t"), "true");

        // Phase 1: simplify away the redundant filter in the logical model.
        var simplified = Simplify(root, new RemoveTrueFilter());
        Assert.IsType<LogicalSource>(simplified);

        // Phase 2: lower the residual.
        var lowered = Lower(simplified, physical, Converters(physical));
        Assert.Equal("t", Assert.IsType<PhysicalSource>(lowered).Table);
    }

    [Fact]
    public void Convention_registers_its_lowering_rules()
    {
        var planner = new HepPlanner(HepProgram.Builder().Build());

        // The physical convention contributes its converters to the planner, rather than the caller
        // listing them by hand.
        RelationalConventions.Physical.Register(planner);

        var logical = TraitSet.CreateEmpty().Plus(RelationalConventions.Logical);
        var physical = TraitSet.CreateEmpty().Plus(RelationalConventions.Physical);
        INode root = new LogicalFilter(logical, new LogicalSource(logical, "t"), "x > 5");

        planner.SetRoot(root);
        planner.ChangeTraits(root, physical);
        var result = planner.FindBestPlan();

        Assert.IsType<PhysicalFilter>(result);
    }

    [Fact]
    public void Incomplete_lowering_throws()
    {
        var (logical, physical) = Setup();

        INode root = new LogicalFilter(logical, new LogicalSource(logical, "t"), "x > 5");

        // Omit the source converter: the source can never reach the physical convention, so the
        // planner cannot satisfy the requested output traits.
        Assert.Throws<CannotPlanException>(() => Lower(root, physical, new FilterConverter(physical)));
    }

    static (TraitSet Logical, TraitSet Physical) Setup()
    {
        return (
            TraitSet.CreateEmpty().Plus(RelationalConventions.Logical),
            TraitSet.CreateEmpty().Plus(RelationalConventions.Physical));
    }

    static IRule[] Converters(TraitSet physical)
    {
        return [new SourceConverter(physical), new FilterConverter(physical), new ParameterConverter(physical)];
    }

    static INode Lower(INode root, TraitSet required, params IRule[] rules)
    {
        var planner = BuildPlanner(rules);
        planner.SetRoot(root);
        planner.ChangeTraits(root, required);
        return planner.FindBestPlan();
    }

    static INode Simplify(INode root, params IRule[] rules)
    {
        var planner = BuildPlanner(rules);
        planner.SetRoot(root);
        return planner.FindBestPlan();
    }

    static HepPlanner BuildPlanner(IRule[] rules)
    {
        var builder = HepProgram.Builder();
        foreach (var rule in rules)
            builder.AddRule(rule);

        return new HepPlanner(builder.Build());
    }

}
