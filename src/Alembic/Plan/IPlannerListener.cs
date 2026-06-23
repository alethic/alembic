using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan;

/// <summary>
/// Listens to events that occur during planning — equivalences discovered, rules attempted and
/// succeeded, nodes discarded, and the nodes chosen for the final plan.
/// </summary>
[Provenance("org.apache.calcite.plan.RelOptListener")]
public interface IPlannerListener
{

    /// <summary>
    /// A node has been registered with an equivalence class.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener", "relEquivalenceFound(RelEquivalenceEvent)")]
    void NodeEquivalenceFound(NodeEquivalenceEvent e);

    /// <summary>
    /// A rule is being applied to a node. Fired twice per match — once before and once after.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener", "ruleAttempted(RuleAttemptedEvent)")]
    void RuleAttempted(RuleAttemptedEvent e);

    /// <summary>
    /// A rule has produced a new equivalent. Fired twice — once before and once after registration.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener", "ruleProductionSucceeded(RuleProductionEvent)")]
    void RuleProductionSucceeded(RuleProductionEvent e);

    /// <summary>
    /// A node is no longer of interest to the planner.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener", "relDiscarded(RelDiscardedEvent)")]
    void NodeDiscarded(NodeDiscardedEvent e);

    /// <summary>
    /// A node has been chosen for the final plan (fired once more with a null node when the plan is
    /// complete).
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener", "relChosen(RelChosenEvent)")]
    void NodeChosen(NodeChosenEvent e);

    /// <summary>
    /// The base for an event about a node; the source is the planner that raised it.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener.RelEvent")]
    abstract class PlannerEvent
    {

        /// <summary>
        /// Initializes the event with its planner and the node it concerns (if any).
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RelEvent", "RelEvent(Object, RelNode)")]
        protected PlannerEvent(IPlanner source, INode? node)
        {
            Source = source;
            Node = node;
        }

        /// <summary>
        /// The planner that raised the event.
        /// </summary>
        public IPlanner Source { get; }

        /// <summary>
        /// The node the event concerns, or <c>null</c>.
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RelEvent", "getRel()")]
        public INode? Node { get; }

    }

    /// <summary>
    /// Indicates that a node has been chosen for the final plan.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener.RelChosenEvent")]
    sealed class NodeChosenEvent : PlannerEvent
    {

        /// <summary>
        /// Creates the event.
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RelChosenEvent", "RelChosenEvent(Object, RelNode)")]
        public NodeChosenEvent(IPlanner source, INode? node)
            : base(source, node)
        {

        }

    }

    /// <summary>
    /// Indicates that a node has joined an equivalence class.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener.RelEquivalenceEvent")]
    sealed class NodeEquivalenceEvent : PlannerEvent
    {

        /// <summary>
        /// Creates the event. <paramref name="equivalenceClass"/> identifies the class the node joined
        /// (the equivalence set, or <c>null</c> for a planner without sets); <paramref name="isPhysical"/>
        /// is whether the node carries a physical (non-logical) convention.
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RelEquivalenceEvent", "RelEquivalenceEvent(Object, RelNode, Object, boolean)")]
        public NodeEquivalenceEvent(IPlanner source, INode node, object? equivalenceClass = null, bool isPhysical = false)
            : base(source, node)
        {
            EquivalenceClass = equivalenceClass;
            IsPhysical = isPhysical;
        }

        /// <summary>
        /// Identifies the equivalence class the node joined, or <c>null</c>.
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RelEquivalenceEvent", "getEquivalenceClass()")]
        public object? EquivalenceClass { get; }

        /// <summary>
        /// Whether the node carries a physical (non-logical) convention.
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RelEquivalenceEvent", "isPhysical()")]
        public bool IsPhysical { get; }

    }

    /// <summary>
    /// Indicates that a node has been discarded.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener.RelDiscardedEvent")]
    sealed class NodeDiscardedEvent : PlannerEvent
    {

        /// <summary>
        /// Creates the event.
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RelDiscardedEvent", "RelDiscardedEvent(Object, RelNode)")]
        public NodeDiscardedEvent(IPlanner source, INode node)
            : base(source, node)
        {

        }

    }

    /// <summary>
    /// The base for an event about a rule application.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener.RuleEvent")]
    abstract class RuleEvent : PlannerEvent
    {

        /// <summary>
        /// Initializes the event with the rule call.
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RuleEvent", "RuleEvent(Object, RelNode, RelOptRuleCall)")]
        protected RuleEvent(IPlanner source, INode? node, RuleCall ruleCall)
            : base(source, node)
        {
            RuleCall = ruleCall;
        }

        /// <summary>
        /// The call the rule is being applied through.
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RuleEvent", "getRuleCall()")]
        public RuleCall RuleCall { get; }

    }

    /// <summary>
    /// Indicates that a rule is being attempted (before or after).
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener.RuleAttemptedEvent")]
    class RuleAttemptedEvent : RuleEvent
    {

        /// <summary>
        /// Creates the event; <paramref name="before"/> is true on the pre-application notification.
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RuleAttemptedEvent", "RuleAttemptedEvent(Object, RelNode, RelOptRuleCall, boolean)")]
        public RuleAttemptedEvent(IPlanner source, INode? node, RuleCall ruleCall, bool before)
            : base(source, node, ruleCall)
        {
            Before = before;
        }

        /// <summary>
        /// Whether this is the notification before the rule is applied.
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RuleAttemptedEvent", "isBefore()")]
        public bool Before { get; }

    }

    /// <summary>
    /// Indicates that a rule has produced a result.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptListener.RuleProductionEvent")]
    sealed class RuleProductionEvent : RuleAttemptedEvent
    {

        /// <summary>
        /// Creates the event.
        /// </summary>
        [Provenance("org.apache.calcite.plan.RelOptListener.RuleProductionEvent", "RuleProductionEvent(Object, RelNode, RelOptRuleCall, boolean)")]
        public RuleProductionEvent(IPlanner source, INode? node, RuleCall ruleCall, bool before)
            : base(source, node, ruleCall, before)
        {

        }

    }

}
