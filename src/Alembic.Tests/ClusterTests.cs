using Alembic.Plan;
using Alembic.Plan.Hep;

using Alembic.Tests.Languages.Relational;

using Xunit;

namespace Alembic.Tests;

public class ClusterTests
{

    [Fact]
    public void Exposes_the_planners_trait_set()
    {
        var planner = new HepPlanner(HepProgram.Builder().Build());
        var cluster = new OpCluster(planner);

        Assert.Equal(planner.EmptyTraitSet, cluster.TraitSet);
    }

    [Fact]
    public void Builds_a_trait_set_from_the_default_plus_given_traits()
    {
        var planner = new HepPlanner(HepProgram.Builder().Build());
        var cluster = new OpCluster(planner);

        var traits = cluster.TraitSetOf(RelationalConventions.Physical);

        Assert.Equal(RelationalConventions.Physical, traits.Convention);
    }

}
