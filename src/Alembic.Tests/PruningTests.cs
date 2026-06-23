using System.Collections.Generic;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;

using Xunit;

namespace Alembic.Tests;

/// <summary>
/// Exercises op pruning: a pruned op is skipped when its queued rule matches come up, so rules no
/// longer fire on it, while the rest of the tree is still expanded.
/// </summary>
public class PruningTests
{

    static readonly OpTraitSet Logical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical);

    [Fact]
    public void A_pruned_op_is_not_expanded_by_rules()
    {
        var spy = new Spy();
        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);
        var source = new LogicalSource(cluster, Logical, "t");
        IOp root = new LogicalFilter(Logical, source, "x > 5");

        planner.AddRule(spy);
        planner.SetRoot(root);

        // Prune the source after registration (which queued a match for it); the queued match is then
        // skipped, so the rule never fires on the source — but it still fires on the un-pruned filter.
        planner.Prune(source);
        planner.FindBestPlan();

        Assert.DoesNotContain(spy.Fired, op => op is LogicalSource);
        Assert.Contains(spy.Fired, op => op is LogicalFilter);
    }

    /// <summary>
    /// A transformation rule that records every op it is invoked on and transforms nothing.
    /// </summary>
    sealed class Spy : OpRule, ITransformationRule
    {

        public Spy()
            : base(Any<IOp>())
        {
        }

        public List<IOp> Fired { get; } = new List<IOp>();

        public override void OnMatch(OpRuleCall call) => Fired.Add(call.Op(0));

    }

}
