using System.Collections.Generic;

namespace Alembic.Plan;

/// <summary>
/// Fans each planner event out to several listeners. A planner holds a single listener; registering more
/// than one wraps them here (port of Calcite's <c>MulticastRelOptListener</c>).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.MulticastRelOptListener")]
public class MulticastPlannerListener : IPlannerListener
{

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.MulticastRelOptListener", "listeners")]
    readonly List<IPlannerListener> _listeners = new List<IPlannerListener>();

    /// <summary>Creates a multicast listener with no constituents.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.MulticastRelOptListener", "MulticastRelOptListener()")]
    public MulticastPlannerListener()
    {
    }

    /// <summary>Adds a listener to the fan-out.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.MulticastRelOptListener", "addListener(RelOptListener)")]
    public void AddListener(IPlannerListener listener) => _listeners.Add(listener);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.MulticastRelOptListener", "relEquivalenceFound(RelEquivalenceEvent)")]
    public void OpEquivalenceFound(IPlannerListener.OpEquivalenceEvent e)
    {
        foreach (var listener in _listeners)
            listener.OpEquivalenceFound(e);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.MulticastRelOptListener", "ruleAttempted(RuleAttemptedEvent)")]
    public void RuleAttempted(IPlannerListener.RuleAttemptedEvent e)
    {
        foreach (var listener in _listeners)
            listener.RuleAttempted(e);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.MulticastRelOptListener", "ruleProductionSucceeded(RuleProductionEvent)")]
    public void RuleProductionSucceeded(IPlannerListener.RuleProductionEvent e)
    {
        foreach (var listener in _listeners)
            listener.RuleProductionSucceeded(e);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.MulticastRelOptListener", "relDiscarded(RelDiscardedEvent)")]
    public void OpDiscarded(IPlannerListener.OpDiscardedEvent e)
    {
        foreach (var listener in _listeners)
            listener.OpDiscarded(e);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.MulticastRelOptListener", "relChosen(RelChosenEvent)")]
    public void OpChosen(IPlannerListener.OpChosenEvent e)
    {
        foreach (var listener in _listeners)
            listener.OpChosen(e);
    }

}
