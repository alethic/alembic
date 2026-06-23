using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan;

/// <summary>
/// Common machinery shared by planners — the registered trait dimensions, the empty trait set built
/// from them, the cost factory, and the rule registry. Concrete planners add the search itself
/// (<see cref="SetRoot"/> / <see cref="FindBestPlan"/>).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner")]
public abstract class AbstractOpPlanner : IOpPlanner
{

    readonly List<OpTraitDef> _traitDefs = new List<OpTraitDef>();
    readonly List<OpRule> _rules = new List<OpRule>();
    readonly Dictionary<string, OpRule> _mapDescToRule = new Dictionary<string, OpRule>();
    readonly List<IPlannerListener> _listeners = new List<IPlannerListener>();
    readonly IOpCostFactory _costFactory;
    OpTraitSet? _emptyTraitSet;
    Regex? _ruleDescExclusionFilter;

    /// <summary>
    /// Initializes the planner with the convention dimension registered (every plan has a convention)
    /// and a cost factory (defaulting to the scalar <see cref="OpCost"/> factory).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "AbstractRelOptPlanner(RelOptCostFactory, Context)")]
    protected AbstractOpPlanner(IOpCostFactory? costFactory = null)
    {
        _costFactory = costFactory ?? OpCost.Factory;
        AddTraitDef(ConventionTraitDef.Instance);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "getRelTraitDefs()")]
    public IReadOnlyList<OpTraitDef> TraitDefs => _traitDefs;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "getCostFactory()")]
    public IOpCostFactory CostFactory => _costFactory;

    /// <summary>
    /// The rules registered with this planner.
    /// </summary>
    protected IReadOnlyList<OpRule> Rules => _rules;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "addRelTraitDef(RelTraitDef)")]
    public void AddTraitDef(OpTraitDef def)
    {
        if (_emptyTraitSet is not null)
            throw new InvalidOperationException("Trait dimensions must be registered before the empty trait set is used.");

        if (!_traitDefs.Contains(def))
            _traitDefs.Add(def);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "emptyTraitSet()")]
    public OpTraitSet EmptyTraitSet
    {
        get
        {
            if (_emptyTraitSet is null)
            {
                var set = OpTraitSet.CreateEmpty();
                foreach (var def in _traitDefs)
                    set = set.Plus(def.Default);

                _emptyTraitSet = set;
            }

            return _emptyTraitSet;
        }
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "addRule(RelOptRule)")]
    public virtual bool AddRule(OpRule rule)
    {
        // Rules are keyed by description, which must be unique. A duplicate registration of the same rule
        // is a no-op; two different rules that happen to share a description is a programming error.
        var description = rule.Description;
        if (_mapDescToRule.TryGetValue(description, out var existing))
        {
            if (existing.Equals(rule))
                return false;

            throw new InvalidOperationException($"Rule descriptions must be unique; existing rule = '{existing.Description}', new rule = '{rule.Description}'.");
        }

        _mapDescToRule.Add(description, rule);
        _rules.Add(rule);
        return true;
    }

    /// <summary>
    /// Removes a previously added rule; returns whether it was registered.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "removeRule(RelOptRule)")]
    public virtual bool RemoveRule(OpRule rule)
    {
        if (!_mapDescToRule.Remove(rule.Description))
            return false;

        _rules.Remove(rule);
        return true;
    }

    /// <summary>
    /// The rule registered under the given description, or <c>null</c> if none.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "getRuleByDescription(String)")]
    protected OpRule? GetRuleByDescription(string description)
    {
        return _mapDescToRule.GetValueOrDefault(description);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "prune(RelNode)")]
    public virtual void Prune(IOp op)
    {
        // The base planner does not model pruning; the cost-based planner overrides this.
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "clear()")]
    public virtual void Clear()
    {
        // The base planner has no search state to reset; concrete planners override to clear theirs.
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "clearRelTraitDefs()")]
    public void ClearTraitDefs()
    {
        _traitDefs.Clear();
        _emptyTraitSet = null;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "setRuleDescExclusionFilter(Pattern)")]
    public void SetRuleDescExclusionFilter(Regex? exclusionFilter)
    {
        _ruleDescExclusionFilter = exclusionFilter;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "isRuleExcluded(RelOptRule)")]
    public bool IsRuleExcluded(OpRule rule)
    {
        if (_ruleDescExclusionFilter is null)
            return false;

        // Calcite uses Matcher.matches(), which requires the *entire* description to match (not a
        // substring); replicate that whole-string anchoring.
        var match = _ruleDescExclusionFilter.Match(rule.Description);
        return match.Success && match.Index == 0 && match.Length == rule.Description.Length;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "addListener(RelOptListener)")]
    public void AddListener(IPlannerListener listener)
    {
        _listeners.Add(listener);
    }

    /// <summary>
    /// Whether any listener is attached. A planner may skip bookkeeping that only a listener observes.
    /// </summary>
    protected internal bool HasListeners => _listeners.Count > 0;

    /// <summary>
    /// Notifies listeners that an op has joined an equivalence class (optionally identified by
    /// <paramref name="equivalenceClass"/>, with <paramref name="isPhysical"/> flagging a physical op).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "notifyEquivalence(RelNode, Object, boolean)")]
    protected internal void FireOpEquivalenceFound(IOp op, object? equivalenceClass = null, bool isPhysical = false)
    {
        foreach (var listener in _listeners)
            listener.OpEquivalenceFound(new IPlannerListener.OpEquivalenceEvent(this, op, equivalenceClass, isPhysical));
    }

    /// <summary>
    /// Notifies listeners that a rule is being attempted (before and after).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "fireRule(RelOptRuleCall)")]
    protected internal void FireRuleAttempted(OpRuleCall call, bool before)
    {
        foreach (var listener in _listeners)
            listener.RuleAttempted(new IPlannerListener.RuleAttemptedEvent(this, call.Op(0), call, before));
    }

    /// <summary>
    /// Notifies listeners that a rule produced an equivalent.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "notifyTransformation(RelOptRuleCall, RelNode, boolean)")]
    protected internal void FireRuleProductionSucceeded(OpRuleCall call, IOp produced, bool before)
    {
        foreach (var listener in _listeners)
            listener.RuleProductionSucceeded(new IPlannerListener.RuleProductionEvent(this, produced, call, before));
    }

    /// <summary>
    /// Notifies listeners that an op has been discarded.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "notifyDiscard(RelNode)")]
    protected internal void FireOpDiscarded(IOp op)
    {
        foreach (var listener in _listeners)
            listener.OpDiscarded(new IPlannerListener.OpDiscardedEvent(this, op));
    }

    /// <summary>
    /// Notifies listeners that an op has been chosen for the final plan (a null op signals the plan
    /// is complete).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "notifyChosen(RelNode)")]
    protected internal void FireOpChosen(IOp? op)
    {
        foreach (var listener in _listeners)
            listener.OpChosen(new IPlannerListener.OpChosenEvent(this, op));
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "setRoot(RelNode)")]
    public abstract void SetRoot(IOp op);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "getRoot()")]
    public abstract IOp? Root { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "changeTraits(RelNode, RelTraitSet)")]
    public abstract IOp ChangeTraits(IOp op, OpTraitSet toTraits);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "findBestExp()")]
    public abstract IOp FindBestPlan();

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "ensureRegistered(RelNode, RelNode)")]
    public abstract IOp EnsureRegistered(IOp op, IOp? equivalent);

}
