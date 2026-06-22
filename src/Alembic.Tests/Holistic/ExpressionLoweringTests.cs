using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Hep;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Expression;
using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Expression.Physical;
using Alembic.Tests.Languages.Expression.Rules;

using Xunit;
using Xunit.Abstractions;

namespace Alembic.Tests.Holistic;

public class ExpressionLoweringTests
{

    readonly ITestOutputHelper _output;

    public ExpressionLoweringTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Lowers_a_sum_of_variables()
    {
        var (logical, physical) = Setup();

        INode root = new Add(logical, new Variable(logical, "a"), new Variable(logical, "b"));

        var result = Lower(root, physical, Converters(physical));

        var add = Assert.IsType<PhysicalAdd>(result);
        Assert.Equal("a", Assert.IsType<PhysicalVariable>(add.Left).Name);
        Assert.Equal("b", Assert.IsType<PhysicalVariable>(add.Right).Name);
    }

    [Fact]
    public void Lowers_a_nested_expression()
    {
        var (logical, physical) = Setup();

        INode root = new Add(logical, new Multiply(logical, new Variable(logical, "a"), new Variable(logical, "b")), new Variable(logical, "c"));

        var result = Lower(root, physical, Converters(physical));

        var add = Assert.IsType<PhysicalAdd>(result);
        Assert.IsType<PhysicalMultiply>(add.Left);
        Assert.Equal("c", Assert.IsType<PhysicalVariable>(add.Right).Name);
    }

    [Fact]
    public void Fuses_a_multiply_add()
    {
        var (logical, physical) = Setup();

        INode root = new Add(logical, new Multiply(logical, new Variable(logical, "a"), new Variable(logical, "b")), new Variable(logical, "c"));

        // Lower, then fuse the physical multiply-then-add into a single fused multiply-add.
        var result = Lower(root, physical, [.. Converters(physical), new FuseMultiplyAdd()]);

        var fma = Assert.IsType<PhysicalFma>(result);
        Assert.Equal("a", Assert.IsType<PhysicalVariable>(fma.A).Name);
        Assert.Equal("b", Assert.IsType<PhysicalVariable>(fma.B).Name);
        Assert.Equal("c", Assert.IsType<PhysicalVariable>(fma.C).Name);
    }

    [Fact]
    public void Folds_a_constant_sum()
    {
        var (logical, _) = Setup();

        INode root = new Add(logical, new Literal(logical, 2), new Literal(logical, 3));

        var result = Simplify(root, new FoldAdd());

        Assert.Equal(5, Assert.IsType<Literal>(result).Value);
    }

    [Fact]
    public void Folds_a_nested_constant_expression()
    {
        var (logical, _) = Setup();

        // (2 * 3) + 4 folds bottom-up to 10.
        INode root = new Add(logical, new Multiply(logical, new Literal(logical, 2), new Literal(logical, 3)), new Literal(logical, 4));

        var result = Simplify(root, new FoldMultiply(), new FoldAdd());

        Assert.Equal(10, Assert.IsType<Literal>(result).Value);
    }

    [Fact]
    public void Folds_then_lowers()
    {
        var (logical, physical) = Setup();

        INode root = new Add(logical, new Literal(logical, 2), new Literal(logical, 3));

        var folded = Simplify(root, new FoldAdd());
        Assert.Equal(5, Assert.IsType<Literal>(folded).Value);

        var lowered = Lower(folded, physical, Converters(physical));
        Assert.Equal(5, Assert.IsType<PhysicalLiteral>(lowered).Value);
    }

    [Fact]
    public void Convention_registers_its_lowering_rules()
    {
        var planner = new HepPlanner(HepProgram.Builder().Build());

        ExpressionConventions.Physical.Register(planner);

        var logical = TraitSet.CreateEmpty().Plus(ExpressionConventions.Logical);
        var physical = TraitSet.CreateEmpty().Plus(ExpressionConventions.Physical);
        INode root = new Multiply(logical, new Variable(logical, "a"), new Literal(logical, 7));

        planner.SetRoot(root);
        planner.ChangeTraits(root, physical);
        var result = planner.FindBestPlan();

        var multiply = Assert.IsType<PhysicalMultiply>(result);
        Assert.Equal("a", Assert.IsType<PhysicalVariable>(multiply.Left).Name);
        Assert.Equal(7, Assert.IsType<PhysicalLiteral>(multiply.Right).Value);
    }

    [Fact]
    public void Incomplete_lowering_throws()
    {
        var (logical, physical) = Setup();

        INode root = new Add(logical, new Variable(logical, "a"), new Variable(logical, "b"));

        // Omit the variable converter: the variables can never reach the physical convention, so the
        // planner cannot satisfy the requested output traits.
        IRule[] rules = [new LiteralConverter(physical), new AddConverter(physical), new MultiplyConverter(physical)];

        Assert.Throws<CannotPlanException>(() => Lower(root, physical, rules));
    }

    static (TraitSet Logical, TraitSet Physical) Setup()
    {
        return (
            TraitSet.CreateEmpty().Plus(ExpressionConventions.Logical),
            TraitSet.CreateEmpty().Plus(ExpressionConventions.Physical));
    }

    static IRule[] Converters(TraitSet physical)
    {
        return [new LiteralConverter(physical), new VariableConverter(physical), new AddConverter(physical), new MultiplyConverter(physical)];
    }

    INode Lower(INode root, TraitSet required, params IRule[] rules)
    {
        var planner = BuildPlanner(rules);
        planner.SetRoot(root);
        planner.ChangeTraits(root, required);
        var best = planner.FindBestPlan();
        _output.WriteLine("--- lowered ---");
        _output.WriteLine(best.ToPlanString());
        return best;
    }

    INode Simplify(INode root, params IRule[] rules)
    {
        var planner = BuildPlanner(rules);
        planner.SetRoot(root);
        var best = planner.FindBestPlan();
        _output.WriteLine("--- simplified ---");
        _output.WriteLine(best.ToPlanString());
        return best;
    }

    static HepPlanner BuildPlanner(IRule[] rules)
    {
        var builder = HepProgram.Builder();
        foreach (var rule in rules)
            builder.AddRule(rule);

        return new HepPlanner(builder.Build());
    }

}
