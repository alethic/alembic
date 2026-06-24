using System.Collections.Generic;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;

using Xunit;
using Xunit.Abstractions;

using Alembic.Algebra.Rules;

namespace Alembic.Tests.Holistic;

/// <summary>
/// Validates the Cascades-specific machinery: the logical/physical rule split, and a physical op's
/// trait pass-through and derivation.
/// </summary>
public class CascadesTests
{

    readonly ITestOutputHelper _output;

    public CascadesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    static readonly OpTraitSet Logical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical);
    static readonly OpTraitSet Physical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Physical);

    [Fact]
    public void A_transformation_rule_fires_on_logical_ops()
    {
        var spy = new SpyTransformationRule();
        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);
        planner.AddRule(spy);
        planner.SetRoot(new LogicalSource(cluster, Logical, "t"));
        planner.FindBestPlan();

        Assert.Contains(spy.Fired, op => op is LogicalSource);
    }

    [Fact]
    public void A_transformation_rule_is_kept_off_physical_ops()
    {
        var spy = new SpyTransformationRule();
        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);
        planner.AddRule(spy);

        // PhysicalSource is an IPhysicalOp, so the dispatch table never offers it to a transformation
        // rule; the rule is never even attempted on it.
        planner.SetRoot(new PhysicalSource(cluster, Physical, "t"));
        planner.FindBestPlan();

        Assert.DoesNotContain(spy.Fired, op => op is PhysicalSource);
    }

    [Fact]
    public void A_physical_op_passes_a_required_trait_set_down_to_its_inputs()
    {
        var planner = new VolcanoPlanner();
        planner.AddTraitDef(SortednessTraitDef.Instance);
        var cluster = new OpCluster(planner);
        var sorted = Physical.Plus(Sortedness.Sorted);

        var filter = new PhysicalFilter(Physical, new PhysicalSource(cluster, Physical, "t"), "x > 5");

        // Pushing a sorted requirement through the filter yields a sorted filter whose input is required
        // sorted in turn.
        var passed = Assert.IsType<PhysicalFilter>(((IPhysicalOp)filter).PassThrough(sorted));
        Assert.Same(Sortedness.Sorted, passed.Traits.Get(SortednessTraitDef.Instance));

        var input = Assert.IsType<OpSubset>(passed.Input);
        Assert.Same(Sortedness.Sorted, input.Traits.Get(SortednessTraitDef.Instance));
    }

    [Fact]
    public void A_physical_op_derives_a_delivered_trait_set_from_its_input()
    {
        var planner = new VolcanoPlanner();
        planner.AddTraitDef(SortednessTraitDef.Instance);
        var cluster = new OpCluster(planner);
        var sorted = Physical.Plus(Sortedness.Sorted);

        var filter = new PhysicalFilter(Physical, new PhysicalSource(cluster, Physical, "t"), "x > 5");

        // Given a sorted input, the filter derives a sorted output.
        var derived = Assert.IsType<PhysicalFilter>(((IPhysicalOp)filter).Derive(sorted, 0));
        Assert.Same(Sortedness.Sorted, derived.Traits.Get(SortednessTraitDef.Instance));
    }

    [Fact]
    public void Top_down_pass_through_delivers_a_required_trait_through_the_tree()
    {
        var unsorted = Physical.Plus(Sortedness.Unsorted);
        var sorted = Physical.Plus(Sortedness.Sorted);

        // The cheapest way to deliver sorted output is to push the requirement down so the filter and its
        // (natively sort-capable) source both deliver sorted (cost 10 + 100), rather than sorting on top
        // with an enforcer (cost 50 + 10 + 100).
        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);
        IOp root = new PhysicalFilter(unsorted, new PhysicalSource(cluster, unsorted, "t"), "x > 5");

        planner.SetTopDownOpt(true);
        planner.AddTraitDef(SortednessTraitDef.Instance);
        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, sorted));
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var filter = Assert.IsType<PhysicalFilter>(best);
        Assert.Same(Sortedness.Sorted, filter.Traits.Get(SortednessTraitDef.Instance));
        var source = Assert.IsType<PhysicalSource>(filter.Input);
        Assert.Same(Sortedness.Sorted, source.Traits.Get(SortednessTraitDef.Instance));
    }

    [Fact]
    public void Top_down_derive_produces_a_sorted_equivalent_bottom_up()
    {
        var unsorted = Physical.Plus(Sortedness.Unsorted);

        var listener = new OpRecordingListener();
        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);
        IOp root = new PhysicalFilter(unsorted, new PhysicalSource(cluster, unsorted, "t"), "x > 5");

        planner.SetTopDownOpt(true);
        planner.AddTraitDef(SortednessTraitDef.Instance);
        planner.AddListener(listener);

        // The source independently offers a sorted scan, so during the (unsorted) search the source's set
        // gains a delivered sorted subset that nobody asked for.
        planner.AddRule(new OfferSortedSource());
        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, unsorted));
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        // Nobody requested sorted, so the chosen plan is the plain unsorted filter.
        var filter = Assert.IsType<PhysicalFilter>(best);
        Assert.Same(Sortedness.Unsorted, filter.Traits.Get(SortednessTraitDef.Instance));

        // But the filter derived a *sorted* filter bottom-up from the sorted source — a novel equivalent
        // the search produced on its own, without any parent requiring it.
        Assert.Contains(listener.Equivalences, op =>
            op is PhysicalFilter && ReferenceEquals(op.Traits.Get(SortednessTraitDef.Instance), Sortedness.Sorted));
    }

    /// <summary>
    /// A transformation rule that records every op it is invoked on and transforms nothing.
    /// </summary>
    sealed class SpyTransformationRule : OpRule, ITransformationRule
    {

        public SpyTransformationRule()
            : base(Any<IOp>())
        {
        }

        public List<IOp> Fired { get; } = new List<IOp>();

        public override void OnMatch(OpRuleCall call) => Fired.Add(call.Op(0));

    }

    /// <summary>
    /// Offers a sorted scan as an equivalent of a physical source (modelling an index). It is an
    /// implementation rule, not a transformation rule, so it applies to the physical source.
    /// </summary>
    sealed class OfferSortedSource : OpRule
    {

        public OfferSortedSource()
            : base(Leaf<PhysicalSource>())
        {
        }

        public override void OnMatch(OpRuleCall call)
        {
            var source = (PhysicalSource)call.Op(0);
            if (ReferenceEquals(source.Traits.Get(SortednessTraitDef.Instance), Sortedness.Sorted))
                return;

            var sorted = source.Traits.Replace(SortednessTraitDef.Instance, Sortedness.Sorted);
            call.TransformTo(new PhysicalSource(source.Cluster, sorted, source.Table));
        }

    }

    /// <summary>
    /// Records every op registered with an equivalence class.
    /// </summary>
    sealed class OpRecordingListener : IPlannerListener
    {

        public List<IOp> Equivalences { get; } = new List<IOp>();

        public void OpEquivalenceFound(IPlannerListener.OpEquivalenceEvent e) => Equivalences.Add(e.Op!);

        public void RuleAttempted(IPlannerListener.RuleAttemptedEvent e) { }

        public void RuleProductionSucceeded(IPlannerListener.RuleProductionEvent e) { }

        public void OpDiscarded(IPlannerListener.OpDiscardedEvent e) { }

        public void OpChosen(IPlannerListener.OpChosenEvent e) { }

    }

}
