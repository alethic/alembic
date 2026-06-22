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
/// Exercises node pruning: a pruned node is skipped when its queued rule matches come up, so rules no
/// longer fire on it, while the rest of the tree is still expanded.
/// </summary>
public class PruningTests
{

    static readonly TraitSet Logical = TraitSet.CreateEmpty().Plus(RelationalConventions.Logical);

    [Fact]
    public void A_pruned_node_is_not_expanded_by_rules()
    {
        var spy = new Spy();
        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        var source = new LogicalSource(cluster, Logical, "t");
        INode root = new LogicalFilter(Logical, source, "x > 5");

        planner.AddRule(spy);
        planner.SetRoot(root);

        // Prune the source after registration (which queued a match for it); the queued match is then
        // skipped, so the rule never fires on the source — but it still fires on the un-pruned filter.
        planner.Prune(source);
        planner.FindBestPlan();

        Assert.DoesNotContain(spy.Fired, node => node is LogicalSource);
        Assert.Contains(spy.Fired, node => node is LogicalFilter);
    }

    /// <summary>
    /// A transformation rule that records every node it is invoked on and transforms nothing.
    /// </summary>
    sealed class Spy : Rule, ITransformationRule
    {

        public Spy()
            : base(Any<INode>())
        {
        }

        public List<INode> Fired { get; } = new List<INode>();

        public override void OnMatch(RuleCall call) => Fired.Add(call.Node(0));

    }

}
