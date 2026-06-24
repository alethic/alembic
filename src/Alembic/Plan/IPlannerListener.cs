using Alembic.Algebra;

namespace Alembic.Plan;

/// <summary>
/// Listens to events that occur during planning — equivalences discovered, rules attempted and
/// succeeded, ops discarded, and the ops chosen for the final plan.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener")]
public interface IPlannerListener
{

    /// <summary>
    /// An op has been registered with an equivalence class.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener", "relEquivalenceFound(RelEquivalenceEvent)")]
    void OpEquivalenceFound(OpEquivalenceEvent e);

    /// <summary>
    /// A rule is being applied to an op. Fired twice per match — once before and once after.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener", "ruleAttempted(RuleAttemptedEvent)")]
    void RuleAttempted(RuleAttemptedEvent e);

    /// <summary>
    /// A rule has produced a new equivalent. Fired twice — once before and once after registration.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener", "ruleProductionSucceeded(RuleProductionEvent)")]
    void RuleProductionSucceeded(RuleProductionEvent e);

    /// <summary>
    /// An op is no longer of interest to the planner.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener", "relDiscarded(RelDiscardedEvent)")]
    void OpDiscarded(OpDiscardedEvent e);

    /// <summary>
    /// An op has been chosen for the final plan (fired once more with a null op when the plan is
    /// complete).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener", "relChosen(RelChosenEvent)")]
    void OpChosen(OpChosenEvent e);

    /// <summary>
    /// The base for an event about an op; the source is the planner that raised it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RelEvent")]
    abstract class PlannerEvent
    {

        /// <summary>
        /// Initializes the event with its planner and the op it concerns (if any).
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RelEvent", "RelEvent(Object, RelNode)")]
        protected PlannerEvent(IOpPlanner source, IOp? op)
        {
            Source = source;
            Op = op;
        }

        /// <summary>
        /// The planner that raised the event.
        /// </summary>
        public IOpPlanner Source { get; }

        /// <summary>
        /// The op the event concerns, or <c>null</c>.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RelEvent", "getRel()")]
        public IOp? Op { get; }

    }

    /// <summary>
    /// Indicates that an op has been chosen for the final plan.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RelChosenEvent")]
    sealed class OpChosenEvent : PlannerEvent
    {

        /// <summary>
        /// Creates the event.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RelChosenEvent", "RelChosenEvent(Object, RelNode)")]
        public OpChosenEvent(IOpPlanner source, IOp? op)
            : base(source, op)
        {

        }

    }

    /// <summary>
    /// Indicates that an op has joined an equivalence class.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RelEquivalenceEvent")]
    sealed class OpEquivalenceEvent : PlannerEvent
    {

        /// <summary>
        /// Creates the event. <paramref name="equivalenceClass"/> identifies the class the op joined
        /// (the equivalence set, or <c>null</c> for a planner without sets); <paramref name="isPhysical"/>
        /// is whether the op carries a physical (non-logical) convention.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RelEquivalenceEvent", "RelEquivalenceEvent(Object, RelNode, Object, boolean)")]
        public OpEquivalenceEvent(IOpPlanner source, IOp op, object? equivalenceClass, bool isPhysical)
            : base(source, op)
        {
            EquivalenceClass = equivalenceClass;
            IsPhysical = isPhysical;
        }

        /// <summary>
        /// Identifies the equivalence class the op joined, or <c>null</c>.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RelEquivalenceEvent", "getEquivalenceClass()")]
        public object? EquivalenceClass { get; }

        /// <summary>
        /// Whether the op carries a physical (non-logical) convention.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RelEquivalenceEvent", "isPhysical()")]
        public bool IsPhysical { get; }

    }

    /// <summary>
    /// Indicates that an op has been discarded.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RelDiscardedEvent")]
    sealed class OpDiscardedEvent : PlannerEvent
    {

        /// <summary>
        /// Creates the event.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RelDiscardedEvent", "RelDiscardedEvent(Object, RelNode)")]
        public OpDiscardedEvent(IOpPlanner source, IOp op)
            : base(source, op)
        {

        }

    }

    /// <summary>
    /// The base for an event about a rule application.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RuleEvent")]
    abstract class RuleEvent : PlannerEvent
    {

        /// <summary>
        /// Initializes the event with the rule call.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RuleEvent", "RuleEvent(Object, RelNode, RelOptRuleCall)")]
        protected RuleEvent(IOpPlanner source, IOp? op, OpRuleCall ruleCall)
            : base(source, op)
        {
            RuleCall = ruleCall;
        }

        /// <summary>
        /// The call the rule is being applied through.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RuleEvent", "getRuleCall()")]
        public OpRuleCall RuleCall { get; }

    }

    /// <summary>
    /// Indicates that a rule is being attempted (before or after).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RuleAttemptedEvent")]
    class RuleAttemptedEvent : RuleEvent
    {

        /// <summary>
        /// Creates the event; <paramref name="before"/> is true on the pre-application notification.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RuleAttemptedEvent", "RuleAttemptedEvent(Object, RelNode, RelOptRuleCall, boolean)")]
        public RuleAttemptedEvent(IOpPlanner source, IOp? op, OpRuleCall ruleCall, bool before)
            : base(source, op, ruleCall)
        {
            Before = before;
        }

        /// <summary>
        /// Whether this is the notification before the rule is applied.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RuleAttemptedEvent", "isBefore()")]
        public bool Before { get; }

    }

    /// <summary>
    /// Indicates that a rule has produced a result.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RuleProductionEvent")]
    sealed class RuleProductionEvent : RuleAttemptedEvent
    {

        /// <summary>
        /// Creates the event.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptListener.RuleProductionEvent", "RuleProductionEvent(Object, RelNode, RelOptRuleCall, boolean)")]
        public RuleProductionEvent(IOpPlanner source, IOp? op, OpRuleCall ruleCall, bool before)
            : base(source, op, ruleCall, before)
        {

        }

    }

}
