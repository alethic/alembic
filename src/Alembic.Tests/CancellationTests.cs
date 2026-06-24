using System;

using Alembic.Plan;
using Alembic.Plan.Hep;
using Alembic.Plan.Volcano;
using Alembic.Util;

using Xunit;

namespace Alembic.Tests;

/// <summary>
/// Exercises cooperative cancellation: a caller requests cancel via the planner's
/// <see cref="CancelFlag"/>, and the planner observes it at <see cref="AbstractOpPlanner.CheckCancel"/>.
/// The cost-based planner throws a <see cref="VolcanoTimeoutException"/> (which its drivers catch),
/// while the base planner throws an <see cref="OperationCanceledException"/>.
/// </summary>
public class CancellationTests
{

    [Fact]
    public void The_volcano_planner_throws_a_timeout_when_cancel_is_requested()
    {
        var planner = new VolcanoPlanner();

        planner.CheckCancel(); // no cancellation requested yet: does nothing

        planner.CancelFlag.RequestCancel();
        Assert.Throws<VolcanoTimeoutException>(() => planner.CheckCancel());

        // The flag can be cleared and reused.
        planner.CancelFlag.ClearCancel();
        planner.CheckCancel();
    }

    [Fact]
    public void The_base_planner_throws_a_cancellation_when_cancel_is_requested()
    {
        var planner = new HepPlanner(HepProgram.Builder().Build());

        planner.CheckCancel();

        planner.CancelFlag.RequestCancel();
        Assert.Throws<OperationCanceledException>(() => planner.CheckCancel());
    }

    [Fact]
    public void A_cancel_flag_supplied_via_the_context_is_the_one_the_planner_uses()
    {
        var flag = new CancelFlag();
        var planner = new VolcanoPlanner(context: Contexts.Of(flag));

        Assert.Same(flag, planner.CancelFlag);

        // Cancelling through the externally held flag aborts the planner.
        flag.RequestCancel();
        Assert.Throws<VolcanoTimeoutException>(() => planner.CheckCancel());
    }

}
