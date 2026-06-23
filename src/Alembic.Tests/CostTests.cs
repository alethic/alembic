using Alembic.Plan;
using Alembic.Plan.Volcano;

using Xunit;

namespace Alembic.Tests;

/// <summary>
/// Exercises the cost-arithmetic helpers (<c>MultiplyBy</c> / <c>DivideBy</c> / <c>IsEqWithEpsilon</c>)
/// on both the scalar <see cref="OpCost"/> and the two-component <see cref="VolcanoCost"/>.
/// </summary>
public class CostTests
{

    [Fact]
    public void Scalar_cost_arithmetic()
    {
        var c = new OpCost(10.0);

        // The scalar magnitude is not exposed (no getRows analog), so costs are compared to each other.
        Assert.True(c.MultiplyBy(2.0).IsEqWithEpsilon(new OpCost(20.0)));
        Assert.Equal(2.0, c.DivideBy(new OpCost(5.0)));
        Assert.True(c.IsEqWithEpsilon(new OpCost(10.0 + 1e-9)));
        Assert.False(c.IsEqWithEpsilon(new OpCost(11.0)));

        // Scaling an infinite cost leaves it infinite.
        Assert.True(((OpCost)new OpCost(double.PositiveInfinity).MultiplyBy(2.0)).IsInfinite);
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
