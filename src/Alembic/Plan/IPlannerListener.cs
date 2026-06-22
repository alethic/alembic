using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan;

/// <summary>
/// Listens to events that occur during planning — equivalences discovered, rules attempted and
/// succeeded, nodes discarded, and the nodes chosen for the final plan.
/// </summary>
public interface IPlannerListener
{

    /// <summary>
    /// A node has been registered with an equivalence class.
    /// </summary>
    void NodeEquivalenceFound(NodeEquivalenceEvent e);

    /// <summary>
    /// A rule is being applied to a node. Fired twice per match — once before and once after.
    /// </summary>
    void RuleAttempted(RuleAttemptedEvent e);

    /// <summary>
    /// A rule has produced a new equivalent. Fired twice — once before and once after registration.
    /// </summary>
    void RuleProductionSucceeded(RuleProductionEvent e);

    /// <summary>
    /// A node is no longer of interest to the planner.
    /// </summary>
    void NodeDiscarded(NodeDiscardedEvent e);

    /// <summary>
    /// A node has been chosen for the final plan (fired once more with a null node when the plan is
    /// complete).
    /// </summary>
    void NodeChosen(NodeChosenEvent e);

    /// <summary>
    /// The base for an event about a node; the source is the planner that raised it.
    /// </summary>
    abstract class PlannerEvent
    {

        /// <summary>
        /// Initializes the event with its planner and the node it concerns (if any).
        /// </summary>
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
        public INode? Node { get; }

    }

    /// <summary>
    /// Indicates that a node has been chosen for the final plan.
    /// </summary>
    sealed class NodeChosenEvent : PlannerEvent
    {

        /// <summary>
        /// Creates the event.
        /// </summary>
        public NodeChosenEvent(IPlanner source, INode? node)
            : base(source, node)
        {

        }

    }

    /// <summary>
    /// Indicates that a node has joined an equivalence class.
    /// </summary>
    sealed class NodeEquivalenceEvent : PlannerEvent
    {

        /// <summary>
        /// Creates the event.
        /// </summary>
        public NodeEquivalenceEvent(IPlanner source, INode node)
            : base(source, node)
        {

        }

    }

    /// <summary>
    /// Indicates that a node has been discarded.
    /// </summary>
    sealed class NodeDiscardedEvent : PlannerEvent
    {

        /// <summary>
        /// Creates the event.
        /// </summary>
        public NodeDiscardedEvent(IPlanner source, INode node)
            : base(source, node)
        {

        }

    }

    /// <summary>
    /// The base for an event about a rule application.
    /// </summary>
    abstract class RuleEvent : PlannerEvent
    {

        /// <summary>
        /// Initializes the event with the rule call.
        /// </summary>
        protected RuleEvent(IPlanner source, INode? node, RuleCall ruleCall)
            : base(source, node)
        {
            RuleCall = ruleCall;
        }

        /// <summary>
        /// The call the rule is being applied through.
        /// </summary>
        public RuleCall RuleCall { get; }

    }

    /// <summary>
    /// Indicates that a rule is being attempted (before or after).
    /// </summary>
    class RuleAttemptedEvent : RuleEvent
    {

        /// <summary>
        /// Creates the event; <paramref name="before"/> is true on the pre-application notification.
        /// </summary>
        public RuleAttemptedEvent(IPlanner source, INode? node, RuleCall ruleCall, bool before)
            : base(source, node, ruleCall)
        {
            Before = before;
        }

        /// <summary>
        /// Whether this is the notification before the rule is applied.
        /// </summary>
        public bool Before { get; }

    }

    /// <summary>
    /// Indicates that a rule has produced a result.
    /// </summary>
    sealed class RuleProductionEvent : RuleAttemptedEvent
    {

        /// <summary>
        /// Creates the event.
        /// </summary>
        public RuleProductionEvent(IPlanner source, INode? node, RuleCall ruleCall, bool before)
            : base(source, node, ruleCall, before)
        {

        }

    }

}
