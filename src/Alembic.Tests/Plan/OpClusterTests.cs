using Alembic.Plan;
using Alembic.Plan.Hep;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational;

using Xunit;

namespace Alembic.Tests.Plan;

public class OpClusterTests
{

    [Fact]
    public void Exposes_the_planners_trait_set()
    {
        var planner = new HepPlanner(HepProgram.Builder().Build());
        var cluster = new OpCluster(planner);

        Assert.Equal(planner.EmptyTraitSet, cluster.TraitSet);
    }

    [Fact]
    public void Builds_a_trait_set_from_the_default_with_given_traits_applied()
    {
        // TraitSetOf replaces each given trait onto its dimension (Calcite's traitSetOf uses replace, not
        // add), so the dimension must be registered: a VolcanoPlanner registers ConventionTraitDef.
        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);

        var traits = cluster.TraitSetOf(RelationalConventions.Physical);

        Assert.Equal(RelationalConventions.Physical, traits.Convention);
    }

    [Fact]
    public void Metadata_query_is_cached_until_invalidated()
    {
        var planner = new HepPlanner(HepProgram.Builder().Build());
        var cluster = new OpCluster(planner);

        var first = cluster.GetMetadataQuery();

        Assert.Same(first, cluster.GetMetadataQuery());

        cluster.InvalidateMetadataQuery();

        Assert.NotSame(first, cluster.GetMetadataQuery());
    }

}
