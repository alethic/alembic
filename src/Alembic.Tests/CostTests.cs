using Alembic.Plan;
using Alembic.Plan.Volcano;

using Xunit;

namespace Alembic.Tests;

/// <summary>
/// Exercises the cost-arithmetic helpers (<c>MultiplyBy</c> / <c>DivideBy</c> / <c>IsEqWithEpsilon</c>)
/// on both the scalar <see cref="Cost"/> and the two-component <see cref="VolcanoCost"/>.
/// </summary>
public class CostTests
{

    [Fact]
    public void Scalar_cost_arithmetic()
    {
        var c = new Cost(10.0);

        Assert.Equal(20.0, ((Cost)c.MultiplyBy(2.0)).Value);
        Assert.Equal(2.0, c.DivideBy(new Cost(5.0)));
        Assert.True(c.IsEqWithEpsilon(new Cost(10.0 + 1e-9)));
        Assert.False(c.IsEqWithEpsilon(new Cost(11.0)));

        // Scaling an infinite cost leaves it infinite (returns the same instance).
        Assert.Same(Cost.Infinity, Cost.Infinity.MultiplyBy(2.0));
    }

    [Fact]
    public void Volcano_cost_arithmetic()
    {
        var c = (VolcanoCost)VolcanoCost.Factory.MakeCost(10.0, 4.0);

        var scaled = (VolcanoCost)c.MultiplyBy(2.0);
        Assert.Equal(20.0, scaled.Cpu);
        Assert.Equal(8.0, scaled.Io);

        // Geometric mean of the per-component ratios: sqrt((10/5) * (4/2)) = 2.
        Assert.Equal(2.0, c.DivideBy(VolcanoCost.Factory.MakeCost(5.0, 2.0)), 9);

        Assert.True(c.IsEqWithEpsilon(VolcanoCost.Factory.MakeCost(10.0, 4.0)));
        Assert.False(c.IsEqWithEpsilon(VolcanoCost.Factory.MakeCost(10.0, 5.0)));
    }

}
