using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;
using Alembic.Tests.Languages.Relational.Rules;

using Xunit;
using Xunit.Abstractions;

namespace Alembic.Tests.Holistic;

public class VolcanoPlanningTests
{

    readonly ITestOutputHelper _output;

    public VolcanoPlanningTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Returns_the_only_plan_when_no_rule_applies()
    {
        var physical = TraitSet.CreateEmpty().Plus(RelationalConventions.Physical);

        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        IOpNode root = new PhysicalFilter(physical, new PhysicalSource(cluster, physical, "t"), "x > 5");

        planner.SetRoot(root);
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var filter = Assert.IsType<PhysicalFilter>(best);
        Assert.Equal("x > 5", filter.Predicate);
        Assert.Equal("t", Assert.IsType<PhysicalSource>(filter.Input).Table);
    }

    [Fact]
    public void Chooses_the_cheaper_equivalent()
    {
        var physical = TraitSet.CreateEmpty().Plus(RelationalConventions.Physical);

        // The push-down rule offers a fused scan-and-filter (cost 100) as an equivalent of the
        // separate filter-over-scan (cost 10 + 100). The planner must pick the cheaper one.
        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        IOpNode root = new PhysicalFilter(physical, new PhysicalSource(cluster, physical, "t"), "x > 5");

        planner.AddRule(new PushFilterIntoSource());
        planner.SetRoot(root);
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var scan = Assert.IsType<PhysicalFilteredSource>(best);
        Assert.Equal("t", scan.Table);
        Assert.Equal("x > 5", scan.Predicate);
    }

    [Fact]
    public void Lowers_a_logical_tree_to_the_requested_convention()
    {
        var (logical, physical) = Setup();

        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        IOpNode root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "x > 5");

        RelationalConventions.Physical.Register(planner);

        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, physical));
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var filter = Assert.IsType<PhysicalFilter>(best);
        Assert.Equal("t", Assert.IsType<PhysicalSource>(filter.Input).Table);
    }

    [Fact]
    public void Lowers_then_chooses_the_cheaper_pushed_down_plan()
    {
        var (logical, physical) = Setup();

        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        IOpNode root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "x > 5");

        RelationalConventions.Physical.Register(planner);
        planner.AddRule(new PushFilterIntoSource());

        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, physical));
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var scan = Assert.IsType<PhysicalFilteredSource>(best);
        Assert.Equal("t", scan.Table);
        Assert.Equal("x > 5", scan.Predicate);
    }

    [Fact]
    public void Throws_when_a_required_conversion_is_missing()
    {
        var (logical, physical) = Setup();

        // No source converter: the source can never reach the physical convention, so the requested
        // physical plan cannot be produced.
        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        IOpNode root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "x > 5");

        planner.AddRule(new FilterConverter(physical));
        planner.AddRule(new ParameterConverter(physical));

        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, physical));

        Assert.Throws<CannotPlanException>(() => planner.FindBestPlan());
    }

    [Fact]
    public void Enforces_a_non_convention_trait_by_inserting_an_enforcer()
    {
        var unsorted = TraitSet.CreateEmpty().Plus(RelationalConventions.Physical).Plus(Sortedness.Unsorted);
        var sorted = TraitSet.CreateEmpty().Plus(RelationalConventions.Physical).Plus(Sortedness.Sorted);

        // The planner is asked for the source sorted; it has no sorted form, so it must insert the
        // sort enforcer — a converter rule whose Source/Target are a trait other than convention.
        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        IOpNode root = new PhysicalSource(cluster, unsorted, "t");

        planner.AddTraitDef(SortednessTraitDef.Instance);
        planner.AddRule(new SortEnforcer());
        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, sorted));
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var sort = Assert.IsType<PhysicalSort>(best);
        Assert.Equal("t", Assert.IsType<PhysicalSource>(sort.Input).Table);
    }

    [Fact]
    public void Top_down_search_lowers_and_chooses_the_cheaper_plan()
    {
        var (logical, physical) = Setup();

        // The same lowering-and-cost-choice scenario as the bottom-up search, but driven top-down.
        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        IOpNode root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "x > 5");

        planner.SetTopDownOpt(true);
        RelationalConventions.Physical.Register(planner);
        planner.AddRule(new PushFilterIntoSource());

        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, physical));
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var scan = Assert.IsType<PhysicalFilteredSource>(best);
        Assert.Equal("t", scan.Table);
        Assert.Equal("x > 5", scan.Predicate);
    }

    [Fact]
    public void Notifies_a_listener_of_planning_events()
    {
        var (logical, physical) = Setup();

        // A lowering scenario: the converters fire (rules produced), and the chosen plan keeps a child
        // (a physical source under the physical filter), so a non-root op is chosen.
        var listener = new CountingListener();
        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        IOpNode root = new LogicalFilter(logical, new LogicalSource(cluster, logical, "t"), "x > 5");

        RelationalConventions.Physical.Register(planner);
        planner.AddListener(listener);
        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, physical));
        planner.FindBestPlan();

        Assert.True(listener.EquivalencesFound > 0);
        Assert.True(listener.RulesProduced > 0);
        Assert.True(listener.OpsChosen > 0);
        Assert.True(listener.PlanCompleted);
    }

    sealed class CountingListener : IPlannerListener
    {
        public int EquivalencesFound { get; private set; }
        public int RulesProduced { get; private set; }
        public int OpsChosen { get; private set; }
        public bool PlanCompleted { get; private set; }

        public void OpEquivalenceFound(IPlannerListener.OpEquivalenceEvent e) => EquivalencesFound++;
        public void RuleAttempted(IPlannerListener.RuleAttemptedEvent e) { }
        public void RuleProductionSucceeded(IPlannerListener.RuleProductionEvent e) => RulesProduced++;
        public void OpDiscarded(IPlannerListener.OpDiscardedEvent e) { }

        public void OpChosen(IPlannerListener.OpChosenEvent e)
        {
            if (e.Op is null)
                PlanCompleted = true;
            else
                OpsChosen++;
        }
    }

    static (TraitSet Logical, TraitSet Physical) Setup()
    {
        return (
            TraitSet.CreateEmpty().Plus(RelationalConventions.Logical),
            TraitSet.CreateEmpty().Plus(RelationalConventions.Physical));
    }

}
