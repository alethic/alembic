using System;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Xunit;

namespace Alembic.Tests.Plan;

/// <summary>
/// The output-type guard the planner uses when adding an op to an equivalence class.
/// </summary>
public class PlanUtilTests
{

    static readonly OpTraitSet Traits = OpTraitSet.CreateEmpty();

    static OpCluster NewCluster() => new OpCluster(new VolcanoPlanner());

    [Fact]
    public void VerifyTypeEquivalence_passes_for_equivalent_output_types()
    {
        var cluster = NewCluster();
        var original = new ShapedLeaf(cluster, Traits, new Shape(2, "a"));
        var added = new ShapedLeaf(cluster, Traits, new Shape(2, "b")); // same width; cosmetic label ignored

        PlanUtil.VerifyTypeEquivalence(original, added, "set"); // does not throw
    }

    [Fact]
    public void VerifyTypeEquivalence_throws_for_mismatched_output_types()
    {
        var cluster = NewCluster();
        var original = new ShapedLeaf(cluster, Traits, new Shape(2));
        var added = new ShapedLeaf(cluster, Traits, new Shape(3));

        Assert.Throws<InvalidOperationException>(() => PlanUtil.VerifyTypeEquivalence(original, added, "set"));
    }

}
