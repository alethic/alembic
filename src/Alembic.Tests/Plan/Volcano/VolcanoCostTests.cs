using Alembic.Plan.Volcano;

using Xunit;

namespace Alembic.Tests.Plan.Volcano;

public class VolcanoCostTests
{

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
