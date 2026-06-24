using System;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Hep;

using Alembic.Tests.Languages.Expression;
using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;

using Xunit;
using Xunit.Abstractions;

namespace Alembic.Tests.Plan;

/// <summary>
/// Exercises operand matching — the child policies (<see cref="RuleOperandChildPolicy"/>) and nested
/// operand patterns — through the planner that consumes them: a rule is run over an op tree by a
/// <see cref="HepPlanner"/>, and the test asserts whether, and how, the operand bound.
/// </summary>
public class OpRuleOperandTests
{

    static readonly OpTraitSet Expr = OpTraitSet.CreateEmpty().Plus(ExpressionConventions.Logical);
    static readonly OpTraitSet Rel = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical);

    readonly ITestOutputHelper _output;

    public OpRuleOperandTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Leaf_matches_regardless_of_children()
    {
        // LEAF (none()) only constrains the operand to have no child operands; like Calcite, it does
        // not require the matched op to be childless. It binds a childless op and an op with children
        // alike.
        Assert.True(Run(SpyRule.LeafOf<LogicalSource>(), c => new LogicalSource(c, Rel, "t")).Fired);
        Assert.True(Run(SpyRule.LeafOf<LogicalFilter>(), c => new LogicalFilter(Rel, new LogicalSource(c, Rel, "t"), "p")).Fired);
    }

    [Fact]
    public void Any_matches_regardless_of_children()
    {
        IOp Filter(OpCluster c) => new LogicalFilter(Rel, new LogicalSource(c, Rel, "t"), "p");

        // Any matches the filter even though it has a child.
        Assert.True(Run(SpyRule.AnyOf<LogicalFilter>(), Filter).Fired);
    }

    [Fact]
    public void Some_matches_children_positionally()
    {
        IOp MakeAdd(OpCluster c) => new Add(Expr, new Variable(c, Expr, "a"), new Literal(c, Expr, 5));

        Assert.True(Run(SpyRule.SomeOf<Add>(SpyRule.LeafOf<Variable>(), SpyRule.LeafOf<Literal>()), MakeAdd).Fired);
        Assert.False(Run(SpyRule.SomeOf<Add>(SpyRule.LeafOf<Literal>(), SpyRule.LeafOf<Variable>()), MakeAdd).Fired);
    }

    [Fact]
    public void Unordered_matches_a_child_in_any_position()
    {
        IOp VarThenLit(OpCluster c) => new Add(Expr, new Variable(c, Expr, "a"), new Literal(c, Expr, 5));
        IOp LitThenVar(OpCluster c) => new Add(Expr, new Literal(c, Expr, 5), new Variable(c, Expr, "a"));

        // A single unordered child operand matches whichever child satisfies it, regardless of position.
        Assert.True(Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Literal>()), VarThenLit).Fired);
        Assert.True(Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Literal>()), LitThenVar).Fired);

        // No child satisfies the operand → no match.
        Assert.False(Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Multiply>()), VarThenLit).Fired);
    }

    [Fact]
    public void Unordered_binds_the_parent_and_the_matched_child()
    {
        IOp VarThenLit(OpCluster c) => new Add(Expr, new Variable(c, Expr, "a"), new Literal(c, Expr, 5));

        // The literal is the right child, but the unordered operand finds it and binds it after the parent.
        var spy = Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Literal>()), VarThenLit);

        Assert.True(spy.Fired);
        Assert.IsType<Add>(spy.Binding[0]);
        Assert.IsType<Literal>(spy.Binding[1]);
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

        IOp root = new LogicalFilter(
            logical,
            new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "inner"),
            "outer");

        planner.SetRoot(root);
        var best = Assert.IsType<LogicalFilter>(planner.FindBestPlan());
        _output.WriteLine(PlanUtil.ToString(best));

        Assert.Equal("outer", best.Predicate);

        var inner = Assert.IsType<LogicalFilter>(best.Input);
        Assert.Equal("tagged", inner.Predicate);
    }

    static SpyRule Run(OpRuleOperand operand, Func<OpCluster, IOp> build)
    {
        var rule = new SpyRule(operand);
        var planner = new HepPlanner(HepProgram.Builder().AddRuleInstance(rule).Build());
        planner.SetRoot(build(new OpCluster(planner)));
        planner.FindBestPlan();
        return rule;
    }

    /// <summary>
    /// A rule that records whether its operand matched and what it bound, transforming nothing. Its nested
    /// factory methods expose the (otherwise <c>protected</c>) operand builders so tests can describe a
    /// pattern.
    /// </summary>
    sealed class SpyRule : OpRule
    {

        public SpyRule(OpRuleOperand operand)
            : base(operand)
        {
        }

        public bool Fired { get; private set; }

        public ImmutableArray<IOp> Binding { get; private set; }

        public override void OnMatch(OpRuleCall call)
        {
            Fired = true;
            Binding = call.GetOpList();
        }

        public static OpRuleOperand AnyOf<TOp>() where TOp : IOp => Operand<TOp>(Any());

        public static OpRuleOperand LeafOf<TOp>() where TOp : IOp => Operand<TOp>(None());

        public static OpRuleOperand SomeOf<TOp>(OpRuleOperand first, params OpRuleOperand[] rest) where TOp : IOp => Operand<TOp>(Some(first, rest));

        public static OpRuleOperand UnorderedOf<TOp>(OpRuleOperand first, params OpRuleOperand[] rest) where TOp : IOp => Operand<TOp>(Unordered(first, rest));

    }

}
