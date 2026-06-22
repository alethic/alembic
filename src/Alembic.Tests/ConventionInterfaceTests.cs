using System;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational.Physical;

using Xunit;

namespace Alembic.Tests;

public class ConventionInterfaceTests
{

    interface IMarked
    {
    }

    [Fact]
    public void A_node_must_implement_its_conventions_interface()
    {
        // A convention that requires its members to implement IMarked, registered on a node that
        // does not — the planner rejects it.
        var marked = new Convention("MARKED", typeof(IMarked));
        var traits = TraitSet.CreateEmpty().Plus(marked);

        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        INode bad = new PhysicalSource(cluster, traits, "t");

        Assert.Throws<InvalidOperationException>(() => planner.SetRoot(bad));
    }

    [Fact]
    public void A_node_in_a_convention_without_a_marker_is_accepted()
    {
        // The default interface is INode, which every node implements, so registration succeeds.
        var plain = new Convention("PLAIN");
        var traits = TraitSet.CreateEmpty().Plus(plain);

        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        INode ok = new PhysicalSource(cluster, traits, "t");

        planner.SetRoot(ok);

        Assert.IsType<PhysicalSource>(planner.FindBestPlan());
    }

}
