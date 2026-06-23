using System;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Hep;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Expression;
using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;

using Xunit;

namespace Alembic.Tests;

/// <summary>
/// Exercises the operand child policies (<see cref="RuleOperandChildPolicy"/>) through the planner that
/// consumes them: a spy rule is run over an op tree by a <see cref="HepPlanner"/>, and the test asserts
/// whether — and how — the operand bound.
/// </summary>
public class OperandPolicyTests
{

    static readonly OpTraitSet Expr = OpTraitSet.CreateEmpty().Plus(ExpressionConventions.Logical);
    static readonly OpTraitSet Rel = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical);

    [Fact]
    public void Leaf_matches_only_childless_ops()
    {
        Assert.True(Run(SpyRule.LeafOf<LogicalSource>(), c => new LogicalSource(c, Rel, "t")).Fired);
        Assert.False(Run(SpyRule.LeafOf<LogicalFilter>(), c => new LogicalFilter(Rel, new LogicalSource(c, Rel, "t"), "p")).Fired);
    }

    [Fact]
    public void Any_matches_regardless_of_children()
    {
        IOpNode Filter(OpCluster c) => new LogicalFilter(Rel, new LogicalSource(c, Rel, "t"), "p");

        // Any matches the filter even though it has a child; Leaf would not.
        Assert.True(Run(SpyRule.AnyOf<LogicalFilter>(), Filter).Fired);
        Assert.False(Run(SpyRule.LeafOf<LogicalFilter>(), Filter).Fired);
    }

    [Fact]
    public void Some_matches_children_positionally()
    {
        IOpNode MakeAdd(OpCluster c) => new Add(Expr, new Variable(c, Expr, "a"), new Literal(c, Expr, 5));

        Assert.True(Run(SpyRule.SomeOf<Add>(SpyRule.LeafOf<Variable>(), SpyRule.LeafOf<Literal>()), MakeAdd).Fired);
        Assert.False(Run(SpyRule.SomeOf<Add>(SpyRule.LeafOf<Literal>(), SpyRule.LeafOf<Variable>()), MakeAdd).Fired);
    }

    [Fact]
    public void Unordered_matches_a_child_in_any_position()
    {
        IOpNode VarThenLit(OpCluster c) => new Add(Expr, new Variable(c, Expr, "a"), new Literal(c, Expr, 5));
        IOpNode LitThenVar(OpCluster c) => new Add(Expr, new Literal(c, Expr, 5), new Variable(c, Expr, "a"));

        // A single unordered child operand matches whichever child satisfies it, regardless of position.
        Assert.True(Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Literal>()), VarThenLit).Fired);
        Assert.True(Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Literal>()), LitThenVar).Fired);

        // No child satisfies the operand → no match.
        Assert.False(Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Multiply>()), VarThenLit).Fired);
    }

    [Fact]
    public void Unordered_binds_the_parent_and_the_matched_child()
    {
        IOpNode VarThenLit(OpCluster c) => new Add(Expr, new Variable(c, Expr, "a"), new Literal(c, Expr, 5));

        // The literal is the right child, but the unordered operand finds it and binds it after the parent.
        var spy = Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Literal>()), VarThenLit);

        Assert.True(spy.Fired);
        Assert.IsType<Add>(spy.Binding[0]);
        Assert.IsType<Literal>(spy.Binding[1]);
    }

    static SpyRule Run(OpRuleOperand operand, Func<OpCluster, IOpNode> build)
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

        public ImmutableArray<IOpNode> Binding { get; private set; }

        public override void OnMatch(OpRuleCall call)
        {
            Fired = true;
            Binding = call.Ops;
        }

        public static OpRuleOperand AnyOf<TOp>() where TOp : IOpNode => Any<TOp>();

        public static OpRuleOperand LeafOf<TOp>() where TOp : IOpNode => Leaf<TOp>();

        public static OpRuleOperand SomeOf<TOp>(params OpRuleOperand[] children) where TOp : IOpNode => Some<TOp>(children);

        public static OpRuleOperand UnorderedOf<TOp>(params OpRuleOperand[] children) where TOp : IOpNode => Unordered<TOp>(children);

    }

}
