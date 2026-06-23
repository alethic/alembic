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
/// consumes them: a spy rule is run over a node tree by a <see cref="HepPlanner"/>, and the test asserts
/// whether — and how — the operand bound.
/// </summary>
public class OperandPolicyTests
{

    static readonly TraitSet Expr = TraitSet.CreateEmpty().Plus(ExpressionConventions.Logical);
    static readonly TraitSet Rel = TraitSet.CreateEmpty().Plus(RelationalConventions.Logical);

    [Fact]
    public void Leaf_matches_only_childless_nodes()
    {
        Assert.True(Run(SpyRule.LeafOf<LogicalSource>(), c => new LogicalSource(c, Rel, "t")).Fired);
        Assert.False(Run(SpyRule.LeafOf<LogicalFilter>(), c => new LogicalFilter(Rel, new LogicalSource(c, Rel, "t"), "p")).Fired);
    }

    [Fact]
    public void Any_matches_regardless_of_children()
    {
        INode Filter(Cluster c) => new LogicalFilter(Rel, new LogicalSource(c, Rel, "t"), "p");

        // Any matches the filter even though it has a child; Leaf would not.
        Assert.True(Run(SpyRule.AnyOf<LogicalFilter>(), Filter).Fired);
        Assert.False(Run(SpyRule.LeafOf<LogicalFilter>(), Filter).Fired);
    }

    [Fact]
    public void Some_matches_children_positionally()
    {
        INode MakeAdd(Cluster c) => new Add(Expr, new Variable(c, Expr, "a"), new Literal(c, Expr, 5));

        Assert.True(Run(SpyRule.SomeOf<Add>(SpyRule.LeafOf<Variable>(), SpyRule.LeafOf<Literal>()), MakeAdd).Fired);
        Assert.False(Run(SpyRule.SomeOf<Add>(SpyRule.LeafOf<Literal>(), SpyRule.LeafOf<Variable>()), MakeAdd).Fired);
    }

    [Fact]
    public void Unordered_matches_a_child_in_any_position()
    {
        INode VarThenLit(Cluster c) => new Add(Expr, new Variable(c, Expr, "a"), new Literal(c, Expr, 5));
        INode LitThenVar(Cluster c) => new Add(Expr, new Literal(c, Expr, 5), new Variable(c, Expr, "a"));

        // A single unordered child operand matches whichever child satisfies it, regardless of position.
        Assert.True(Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Literal>()), VarThenLit).Fired);
        Assert.True(Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Literal>()), LitThenVar).Fired);

        // No child satisfies the operand → no match.
        Assert.False(Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Multiply>()), VarThenLit).Fired);
    }

    [Fact]
    public void Unordered_binds_the_parent_and_the_matched_child()
    {
        INode VarThenLit(Cluster c) => new Add(Expr, new Variable(c, Expr, "a"), new Literal(c, Expr, 5));

        // The literal is the right child, but the unordered operand finds it and binds it after the parent.
        var spy = Run(SpyRule.UnorderedOf<Add>(SpyRule.LeafOf<Literal>()), VarThenLit);

        Assert.True(spy.Fired);
        Assert.IsType<Add>(spy.Binding[0]);
        Assert.IsType<Literal>(spy.Binding[1]);
    }

    static SpyRule Run(RuleOperand operand, Func<Cluster, INode> build)
    {
        var rule = new SpyRule(operand);
        var planner = new HepPlanner(HepProgram.Builder().AddRuleInstance(rule).Build());
        planner.SetRoot(build(new Cluster(planner)));
        planner.FindBestPlan();
        return rule;
    }

    /// <summary>
    /// A rule that records whether its operand matched and what it bound, transforming nothing. Its nested
    /// factory methods expose the (otherwise <c>protected</c>) operand builders so tests can describe a
    /// pattern.
    /// </summary>
    sealed class SpyRule : Rule
    {

        public SpyRule(RuleOperand operand)
            : base(operand)
        {
        }

        public bool Fired { get; private set; }

        public ImmutableArray<INode> Binding { get; private set; }

        public override void OnMatch(RuleCall call)
        {
            Fired = true;
            Binding = call.Nodes;
        }

        public static RuleOperand AnyOf<TNode>() where TNode : INode => Any<TNode>();

        public static RuleOperand LeafOf<TNode>() where TNode : INode => Leaf<TNode>();

        public static RuleOperand SomeOf<TNode>(params RuleOperand[] children) where TNode : INode => Some<TNode>(children);

        public static RuleOperand UnorderedOf<TNode>(params RuleOperand[] children) where TNode : INode => Unordered<TNode>(children);

    }

}
