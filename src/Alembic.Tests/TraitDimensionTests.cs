using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Xunit;

namespace Alembic.Tests;

public class TraitDimensionTests
{

    [Fact]
    public void Second_dimension_reads_and_interns()
    {
        // Build a set with convention plus a second dimension, via createEmpty + plus.
        var empty = OpTraitSet.CreateEmpty().Plus(Convention.None).Plus(Sortedness.Unsorted);
        var sorted = empty.Replace(SortednessTraitDef.Instance, Sortedness.Sorted);

        Assert.Equal(Sortedness.Unsorted, empty.Get(SortednessTraitDef.Instance));
        Assert.Equal(Sortedness.Sorted, sorted.Get(SortednessTraitDef.Instance));

        // Convention is still present alongside the second dimension.
        Assert.Equal(Convention.None, sorted.Convention);

        // Interning holds within the shared cache.
        var sortedAgain = empty.Replace(SortednessTraitDef.Instance, Sortedness.Sorted);
        Assert.Same(sorted, sortedAgain);
    }

    [Fact]
    public void Satisfies_is_a_partial_order()
    {
        Assert.True(Sortedness.Sorted.Satisfies(Sortedness.Sorted));
        Assert.True(Sortedness.Sorted.Satisfies(Sortedness.Unsorted));
        Assert.False(Sortedness.Unsorted.Satisfies(Sortedness.Sorted));
    }

    [Fact]
    public void A_dimension_registered_after_use_takes_effect()
    {
        var planner = new VolcanoPlanner();

        // Using the empty trait set does not freeze the registry: Calcite recomputes emptyTraitSet() on
        // each call, so a dimension registered afterwards still appears.
        _ = planner.EmptyTraitSet;
        planner.AddTraitDef(SortednessTraitDef.Instance);

        Assert.True(planner.EmptyTraitSet.IsEnabled(SortednessTraitDef.Instance));
    }

}
