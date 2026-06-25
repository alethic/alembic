using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

using Alembic.Algebra;
using Alembic.Algebra.Rules;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;

using Xunit;

namespace Alembic.Tests.Plan.Volcano;

public class VolcanoPlannerTests
{

    static readonly OpTraitSet Logical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical);
    static readonly OpTraitSet Physical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Physical);

    [Fact]
    public void Throws_a_timeout_when_cancellation_is_requested()
    {
        var cts = new CancellationTokenSource();
        var planner = new VolcanoPlanner(context: Contexts.Of(cts.Token));

        Assert.Equal(cts.Token, planner.CancellationToken);
        planner.CheckCancel(); // not cancelled yet: does nothing

        cts.Cancel();
        Assert.Throws<VolcanoTimeoutException>(() => planner.CheckCancel());
    }

    [Fact]
    public void A_pruned_op_is_not_expanded_by_rules()
    {
        var spy = new Spy();
        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);
        var source = new LogicalSource(cluster, Logical, "t");
        IOp root = new LogicalFilter(Logical, source, "x > 5");

        planner.AddRule(spy);
        planner.SetRoot(root);

        // Prune the source after registration (which queued a match for it); the queued match is then
        // skipped, so the rule never fires on the source — but it still fires on the un-pruned filter.
        planner.Prune(source);
        planner.FindBestPlan();

        Assert.DoesNotContain(spy.Fired, op => op is LogicalSource);
        Assert.Contains(spy.Fired, op => op is LogicalFilter);
    }

    [Fact]
    public void A_dimension_registered_after_use_takes_effect()
    {
        var planner = new VolcanoPlanner();

        _ = planner.EmptyTraitSet;
        planner.AddTraitDef(SortednessTraitDef.Instance);

        Assert.True(planner.EmptyTraitSet.IsEnabled(SortednessTraitDef.Instance));
    }

    // The rule-exclusion filter is a Volcano feature (Calcite's HEP does not consult it); it is wired
    // into VolcanoRuleCall.OnMatch, so an excluded rule's match is skipped before it can fire.
    [Fact]
    public void An_excluded_rule_does_not_fire()
    {
        var spy = new SpyRule();
        var planner = new VolcanoPlanner();
        planner.AddRule(spy);
        planner.SetRuleDescExclusionFilter(new Regex("SpyRule"));

        var cluster = new OpCluster(planner);
        planner.SetRoot(new PhysicalSource(cluster, Physical, "t"));
        planner.FindBestPlan();

        Assert.False(spy.Fired);
    }

    [Fact]
    public void Clearing_the_exclusion_filter_lets_the_rule_fire_again()
    {
        var spy = new SpyRule();
        var planner = new VolcanoPlanner();
        planner.AddRule(spy);
        planner.SetRuleDescExclusionFilter(new Regex("SpyRule"));
        planner.SetRuleDescExclusionFilter(null);

        var cluster = new OpCluster(planner);
        planner.SetRoot(new PhysicalSource(cluster, Physical, "t"));
        planner.FindBestPlan();

        Assert.True(spy.Fired);
    }

    // Registering an op as equivalent to another with a different output type is rejected: a set holds
    // only ops with the same output (Calcite's register row-type guard).
    [Fact]
    public void Register_rejects_an_equivalent_with_a_different_output_type()
    {
        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);
        var op = new ShapedLeaf(cluster, planner.EmptyTraitSet, new Shape(2));
        var equivalent = new ShapedLeaf(cluster, planner.EmptyTraitSet, new Shape(3));

        Assert.Throws<ArgumentException>(() => planner.Register(op, equivalent));
    }

    // End to end: a rule that offers an "equivalent" with a different output type is rejected when the
    // planner registers the result during the search — the set's output-type invariant holds mid-plan.
    [Fact]
    public void A_rule_transforming_to_a_different_output_type_is_rejected_during_planning()
    {
        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);

        planner.AddRule(new Retype());
        planner.SetRoot(new ShapedLeaf(cluster, planner.EmptyTraitSet, new Shape(2)));

        // The planner wraps a failing rule application; the cause is the output-type guard.
        var error = Assert.Throws<InvalidOperationException>(() => planner.FindBestPlan());
        var cause = Assert.IsType<ArgumentException>(error.InnerException);
        Assert.Contains("output type", cause.Message);
    }

    // Offers, as an equivalent of the matched leaf, a leaf with a different output type.
    sealed class Retype : OpRule
    {

        public Retype()
            : base(Operand<ShapedLeaf>(Any()))
        {
        }

        public override void OnMatch(OpRuleCall call)
        {
            var leaf = call.Op(0);
            call.TransformTo(new ShapedLeaf(leaf.Cluster, leaf.Traits, new Shape(3)));
        }

    }

    /// <summary>
    /// A transformation rule that records every op it is invoked on and transforms nothing.
    /// </summary>
    sealed class Spy : OpRule, ITransformationRule
    {

        public Spy()
            : base(Operand<IOp>(Any()))
        {
        }

        public List<IOp> Fired { get; } = new List<IOp>();

        public override void OnMatch(OpRuleCall call) => Fired.Add(call.Op(0));

    }

    // Matches any op and records whether it was ever given the chance to fire.
    sealed class SpyRule : OpRule
    {

        public bool Fired;

        public SpyRule()
            : base(Operand<IOp>(Any()))
        {
        }

        public override void OnMatch(OpRuleCall call)
        {
            Fired = true;
        }

    }

}
