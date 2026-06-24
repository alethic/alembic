using System;
using System.Threading;

using Alembic.Plan;
using Alembic.Plan.Hep;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational.Logical;

using Xunit;

namespace Alembic.Tests.Plan;

public class AbstractOpPlannerTests
{

    [Fact]
    public void Throws_a_cancellation_when_cancellation_is_requested()
    {
        var cts = new CancellationTokenSource();
        var planner = new HepPlanner(HepProgram.Builder().Build(), Contexts.Of(cts.Token));

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

    [Fact]
    public void Adding_a_duplicate_rule_is_a_no_op()
    {
        var planner = new VolcanoPlanner();

        // Two instances are equal (same type, description, and operand), so the second registration is
        // rejected.
        Assert.True(planner.AddRule(new NoOp()));
        Assert.False(planner.AddRule(new NoOp()));

        // Once removed, the description is free again.
        Assert.True(planner.RemoveRule(new NoOp()));
        Assert.True(planner.AddRule(new NoOp()));
    }

    [Fact]
    public void A_locked_planner_rejects_new_rules()
    {
        var planner = new VolcanoPlanner();
        planner.SetLocked(true);
        Assert.False(planner.AddRule(new NoOp()));

        planner.SetLocked(false);
        Assert.True(planner.AddRule(new NoOp()));
    }

    [Fact]
    public void Clear_resets_the_rule_registry()
    {
        var planner = new VolcanoPlanner();
        Assert.True(planner.AddRule(new NoOp()));

        planner.Clear();

        // After a reset the description is free again, so the same rule can be re-registered.
        Assert.True(planner.AddRule(new NoOp()));
    }

    sealed class NoOp : OpRule
    {

        public NoOp()
            : base(Leaf<LogicalSource>())
        {
        }

        public override void OnMatch(OpRuleCall call)
        {
        }

    }

}
