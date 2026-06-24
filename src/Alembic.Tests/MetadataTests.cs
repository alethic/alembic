using Alembic.Algebra.Metadata;
using Alembic.Plan;
using Alembic.Plan.Hep;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Physical;

using Xunit;

namespace Alembic.Tests;

/// <summary>
/// Exercises the cost metadata subsystem: the cumulative/non-cumulative cost handlers, the per-query
/// cache on the cluster, and the lower-bound cost used for top-down pruning.
/// </summary>
public class MetadataTests
{

    static bool CostEquals(IOpCost a, IOpCost b) => !a.IsLessThan(b) && !b.IsLessThan(a);

    [Fact]
    public void NonCumulative_cost_is_the_ops_own_cost()
    {
        var planner = new HepPlanner(HepProgram.Builder().Build());
        var cluster = new OpCluster(planner);
        var traits = cluster.TraitSetOf(RelationalConventions.Physical);
        var source = new PhysicalSource(cluster, traits, "t");

        var mq = cluster.GetMetadataQuery();

        Assert.True(CostEquals(mq.GetNonCumulativeCost(source)!, source.ComputeSelfCost(planner, mq)));
    }

    [Fact]
    public void Cumulative_cost_adds_the_inputs()
    {
        var planner = new HepPlanner(HepProgram.Builder().Build());
        var cluster = new OpCluster(planner);
        var traits = cluster.TraitSetOf(RelationalConventions.Physical);
        var source = new PhysicalSource(cluster, traits, "t");
        var filter = new PhysicalFilter(traits, source, "p");

        var mq = cluster.GetMetadataQuery();

        var expected = mq.GetNonCumulativeCost(filter)!.Plus(mq.GetCumulativeCost(source)!);

        Assert.True(CostEquals(mq.GetCumulativeCost(filter)!, expected));
        // The cumulative cost strictly exceeds the filter's own cost, since it folds in the source.
        Assert.True(mq.GetNonCumulativeCost(filter)!.IsLessThan(mq.GetCumulativeCost(filter)!));
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

    [Fact]
    public void Parallelism_has_generic_defaults()
    {
        var planner = new HepPlanner(HepProgram.Builder().Build());
        var cluster = new OpCluster(planner);
        var traits = cluster.TraitSetOf(RelationalConventions.Physical);
        var source = new PhysicalSource(cluster, traits, "t");

        var mq = cluster.GetMetadataQuery();

        Assert.False(mq.IsPhaseTransition(source));
        Assert.Equal(1, mq.SplitCount(source));
    }

    [Fact]
    public void Memory_is_unknown_by_default()
    {
        var planner = new HepPlanner(HepProgram.Builder().Build());
        var cluster = new OpCluster(planner);
        var traits = cluster.TraitSetOf(RelationalConventions.Physical);
        var source = new PhysicalSource(cluster, traits, "t");

        var mq = cluster.GetMetadataQuery();

        // No op reports its memory, so the cumulative figures are unknown too.
        Assert.Null(mq.Memory(source));
        Assert.Null(mq.CumulativeMemoryWithinPhase(source));
        Assert.Null(mq.CumulativeMemoryWithinPhaseSplit(source));
    }

    [Fact]
    public void Lower_bound_cost_sums_self_and_inputs()
    {
        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);
        var traits = cluster.TraitSetOf(RelationalConventions.Physical);
        var source = new PhysicalSource(cluster, traits, "t");
        var filter = new PhysicalFilter(traits, source, "p");

        var mq = cluster.GetMetadataQuery();

        var expected = mq.GetNonCumulativeCost(filter)!.Plus(mq.GetLowerBoundCost(source, planner)!);

        Assert.True(CostEquals(mq.GetLowerBoundCost(filter, planner)!, expected));
    }

}
