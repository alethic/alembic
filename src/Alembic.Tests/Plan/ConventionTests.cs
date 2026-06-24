using System;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational.Physical;

using Xunit;

namespace Alembic.Tests.Plan;

public class ConventionTests
{

    interface IMarked
    {
    }

    [Fact]
    public void A_op_must_implement_its_conventions_interface()
    {
        // A convention that requires its members to implement IMarked, registered on an op that
        // does not — the planner rejects it.
        var marked = new Convention("MARKED", typeof(IMarked));
        var traits = OpTraitSet.CreateEmpty().Plus(marked);

        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);
        IOp bad = new PhysicalSource(cluster, traits, "t");

        Assert.Throws<InvalidOperationException>(() => planner.SetRoot(bad));
    }

    [Fact]
    public void A_op_in_a_convention_without_a_marker_is_accepted()
    {
        // The default interface is IOp, which every op implements, so registration succeeds.
        var plain = new Convention("PLAIN", typeof(IOp));
        var traits = OpTraitSet.CreateEmpty().Plus(plain);

        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);
        IOp ok = new PhysicalSource(cluster, traits, "t");

        planner.SetRoot(ok);

        Assert.IsType<PhysicalSource>(planner.FindBestPlan());
    }

}
