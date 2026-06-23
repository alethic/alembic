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

        var planner = BuildPlanner(Converters(physical));
        var cluster = new Cluster(planner);
        IOpNode root = new Add(logical, new Variable(cluster, logical, "a"), new Variable(cluster, logical, "b"));

        var result = Lower(planner, root, physical);

        var add = Assert.IsType<PhysicalAdd>(result);
        Assert.Equal("a", Assert.IsType<PhysicalVariable>(add.Left).Name);
        Assert.Equal("b", Assert.IsType<PhysicalVariable>(add.Right).Name);
    }

    [Fact]
    public void Lowers_a_nested_expression()
    {
        var (logical, physical) = Setup();

        var planner = BuildPlanner(Converters(physical));
        var cluster = new Cluster(planner);
        IOpNode root = new Add(logical, new Multiply(logical, new Variable(cluster, logical, "a"), new Variable(cluster, logical, "b")), new Variable(cluster, logical, "c"));

        var result = Lower(planner, root, physical);

        var add = Assert.IsType<PhysicalAdd>(result);
        Assert.IsType<PhysicalMultiply>(add.Left);
        Assert.Equal("c", Assert.IsType<PhysicalVariable>(add.Right).Name);
    }

    [Fact]
    public void Fuses_a_multiply_add()
    {
        var (logical, physical) = Setup();

        // Lower, then fuse the physical multiply-then-add into a single fused multiply-add.
        var planner = BuildPlanner([.. Converters(physical), new FuseMultiplyAdd()]);
        var cluster = new Cluster(planner);
        IOpNode root = new Add(logical, new Multiply(logical, new Variable(cluster, logical, "a"), new Variable(cluster, logical, "b")), new Variable(cluster, logical, "c"));

        var result = Lower(planner, root, physical);

        var fma = Assert.IsType<PhysicalFma>(result);
        Assert.Equal("a", Assert.IsType<PhysicalVariable>(fma.A).Name);
        Assert.Equal("b", Assert.IsType<PhysicalVariable>(fma.B).Name);
        Assert.Equal("c", Assert.IsType<PhysicalVariable>(fma.C).Name);
    }

    [Fact]
    public void Folds_a_constant_sum()
    {
        var (logical, _) = Setup();

        var planner = BuildPlanner(new FoldAdd());
        var cluster = new Cluster(planner);
        IOpNode root = new Add(logical, new Literal(cluster, logical, 2), new Literal(cluster, logical, 3));

        var result = Simplify(planner, root);

        Assert.Equal(5, Assert.IsType<Literal>(result).Value);
    }

    [Fact]
    public void Folds_a_nested_constant_expression()
    {
        var (logical, _) = Setup();

        // (2 * 3) + 4 folds bottom-up to 10.
        var planner = BuildPlanner(new FoldMultiply(), new FoldAdd());
        var cluster = new Cluster(planner);
        IOpNode root = new Add(logical, new Multiply(logical, new Literal(cluster, logical, 2), new Literal(cluster, logical, 3)), new Literal(cluster, logical, 4));

        var result = Simplify(planner, root);

        Assert.Equal(10, Assert.IsType<Literal>(result).Value);
    }

    [Fact]
    public void Folds_then_lowers()
    {
        var (logical, physical) = Setup();

        var planner = BuildPlanner(new FoldAdd());
        var cluster = new Cluster(planner);
        IOpNode root = new Add(logical, new Literal(cluster, logical, 2), new Literal(cluster, logical, 3));

        var folded = Simplify(planner, root);
        Assert.Equal(5, Assert.IsType<Literal>(folded).Value);

        var lowered = Lower(BuildPlanner(Converters(physical)), folded, physical);
        Assert.Equal(5, Assert.IsType<PhysicalLiteral>(lowered).Value);
    }

    [Fact]
    public void Convention_registers_its_lowering_rules()
    {
        // The program applies every converter rule; the physical convention contributes them to the
        // planner, rather than the caller listing them by hand.
        var planner = new HepPlanner(HepProgram.Builder().AddRuleClass<ConverterRule>().Build());
        var cluster = new Cluster(planner);

        ExpressionConventions.Physical.Register(planner);

        var logical = TraitSet.CreateEmpty().Plus(ExpressionConventions.Logical);
        var physical = TraitSet.CreateEmpty().Plus(ExpressionConventions.Physical);
        IOpNode root = new Multiply(logical, new Variable(cluster, logical, "a"), new Literal(cluster, logical, 7));

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

        // Omit the variable converter: the variables can never reach the physical convention, so the
        // planner cannot satisfy the requested output traits.
        var planner = BuildPlanner(new LiteralConverter(physical), new AddConverter(physical), new MultiplyConverter(physical));
        var cluster = new Cluster(planner);
        IOpNode root = new Add(logical, new Variable(cluster, logical, "a"), new Variable(cluster, logical, "b"));

        Assert.Throws<CannotPlanException>(() => Lower(planner, root, physical));
    }

    static (TraitSet Logical, TraitSet Physical) Setup()
    {
        return (
            TraitSet.CreateEmpty().Plus(ExpressionConventions.Logical),
            TraitSet.CreateEmpty().Plus(ExpressionConventions.Physical));
    }

    static Rule[] Converters(TraitSet physical)
    {
        return [new LiteralConverter(physical), new VariableConverter(physical), new AddConverter(physical), new MultiplyConverter(physical)];
    }

    IOpNode Lower(HepPlanner planner, IOpNode root, TraitSet required)
    {
        planner.SetRoot(root);
        planner.ChangeTraits(root, required);
        var best = planner.FindBestPlan();
        _output.WriteLine("--- lowered ---");
        _output.WriteLine(PlanUtil.ToString(best));
        return best;
    }

    IOpNode Simplify(HepPlanner planner, IOpNode root)
    {
        planner.SetRoot(root);
        var best = planner.FindBestPlan();
        _output.WriteLine("--- simplified ---");
        _output.WriteLine(PlanUtil.ToString(best));
        return best;
    }

    static HepPlanner BuildPlanner(params Rule[] rules)
    {
        // Fire all the rules together to a fixed point (a rule collection), so lowering can cascade
        // across them in one instruction.
        var builder = HepProgram.Builder().AddRuleCollection(rules);
        return new HepPlanner(builder.Build());
    }

}
