using System.Collections.Generic;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;
using Alembic.Tests.Languages.Relational.Rules;

using Xunit;
using Xunit.Abstractions;

namespace Alembic.Tests.Holistic;

public class MultipleEquivalentsTests
{

    readonly ITestOutputHelper _output;

    public MultipleEquivalentsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void A_rule_registers_several_equivalents_and_the_cheapest_is_chosen()
    {
        var logical = TraitSet.CreateEmpty().Plus(RelationalConventions.Logical);
        var physical = TraitSet.CreateEmpty().Plus(RelationalConventions.Physical);

        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        INode root = new LogicalSource(cluster, logical, "t");

        planner.AddRule(new OfferTwoScans(physical));
        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, physical));
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        // The rule offered the index scan (cost 50) before the full scan (cost 100); both were
        // registered, and the planner chose the cheaper one despite it being registered first (so it
        // is not "last equivalent wins").
        var scan = Assert.IsType<PhysicalIndexSource>(best);
        Assert.Equal("t", scan.Table);
    }

    [Fact]
    public void A_rule_registers_a_secondary_equivalence_through_the_equiv_map()
    {
        var logical = TraitSet.CreateEmpty().Plus(RelationalConventions.Logical);

        var listener = new RecordingListener();
        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        INode root = new LogicalSource(cluster, logical, "t");

        // The rule registers a primary equivalent for the matched source, and — in the same call, via the
        // equivalence map — a second expression declared equivalent to the matched source.
        planner.AddListener(listener);
        planner.AddRule(new OfferPrimaryAndSecondary());
        planner.SetRoot(root);
        planner.FindBestPlan();

        // Both the primary and the map-supplied secondary equivalent were registered.
        Assert.Contains(listener.Equivalences, node => node is LogicalSource { Table: "t_primary" });
        Assert.Contains(listener.Equivalences, node => node is LogicalSource { Table: "t_secondary" });
    }

    /// <summary>
    /// On the source "t", registers a primary equivalent plus a secondary equivalence passed through the
    /// equivalence map. Guarded to fire only on "t", so the equivalents it produces do not re-trigger it.
    /// </summary>
    sealed class OfferPrimaryAndSecondary : Rule
    {

        public OfferPrimaryAndSecondary()
            : base(Leaf<LogicalSource>())
        {
        }

        public override void OnMatch(RuleCall call)
        {
            var source = (LogicalSource)call.Node(0);
            if (source.Table != "t")
                return;

            var primary = new LogicalSource(source.Cluster, source.Traits, "t_primary");
            var secondary = new LogicalSource(source.Cluster, source.Traits, "t_secondary");
            call.TransformTo(primary, new Dictionary<INode, INode> { [secondary] = source });
        }

    }

    sealed class RecordingListener : IPlannerListener
    {

        public List<INode> Equivalences { get; } = new List<INode>();

        public void NodeEquivalenceFound(IPlannerListener.NodeEquivalenceEvent e) => Equivalences.Add(e.Node!);

        public void RuleAttempted(IPlannerListener.RuleAttemptedEvent e) { }

        public void RuleProductionSucceeded(IPlannerListener.RuleProductionEvent e) { }

        public void NodeDiscarded(IPlannerListener.NodeDiscardedEvent e) { }

        public void NodeChosen(IPlannerListener.NodeChosenEvent e) { }

    }

}
