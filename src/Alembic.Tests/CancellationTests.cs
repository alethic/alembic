using System;
using System.Threading;

using Alembic.Plan;
using Alembic.Plan.Hep;
using Alembic.Plan.Volcano;

using Xunit;

namespace Alembic.Tests;

/// <summary>
/// Exercises cooperative cancellation: a caller supplies a <see cref="CancellationTokenSource"/> through
/// the planner's <see cref="IContext"/> and cancels it; the planner observes the token at
/// <see cref="AbstractOpPlanner.CheckCancel"/>. The cost-based planner throws a
/// <see cref="VolcanoTimeoutException"/> (which its drivers catch); the base planner throws an
/// <see cref="OperationCanceledException"/>.
/// </summary>
public class CancellationTests
{

    [Fact]
    public void The_volcano_planner_throws_a_timeout_when_cancellation_is_requested()
    {
        var cts = new CancellationTokenSource();
        var planner = new VolcanoPlanner(context: Contexts.Of(cts));

        Assert.Equal(cts.Token, planner.CancellationToken);
        planner.CheckCancel(); // not cancelled yet: does nothing

        cts.Cancel();
        Assert.Throws<VolcanoTimeoutException>(() => planner.CheckCancel());
    }

    [Fact]
    public void The_base_planner_throws_a_cancellation_when_cancellation_is_requested()
    {
        var cts = new CancellationTokenSource();
        var planner = new HepPlanner(HepProgram.Builder().Build(), Contexts.Of(cts));

        planner.CheckCancel();

        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => planner.CheckCancel());
    }

    [Fact]
    public void A_planner_with_no_cancellation_source_never_cancels()
    {
        var planner = new VolcanoPlanner();

        Assert.Equal(CancellationToken.None, planner.CancellationToken);
        planner.CheckCancel(); // CancellationToken.None is never cancelled
    }

}
