using Alembic.Plan;

using Xunit;

namespace Alembic.Tests.Plan;

public class OpCostTests
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

}
