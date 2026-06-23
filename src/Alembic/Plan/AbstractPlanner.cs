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
[Provenance("org.apache.calcite.plan.AbstractRelOptPlanner")]
public abstract class AbstractPlanner : IPlanner
{

    readonly List<TraitDef> _traitDefs = new List<TraitDef>();
    readonly List<Rule> _rules = new List<Rule>();
    readonly Dictionary<string, Rule> _mapDescToRule = new Dictionary<string, Rule>();
    readonly List<IPlannerListener> _listeners = new List<IPlannerListener>();
    readonly ICostFactory _costFactory;
    TraitSet? _emptyTraitSet;
    Regex? _ruleDescExclusionFilter;

    /// <summary>
    /// Initializes the planner with the convention dimension registered (every plan has a convention)
    /// and a cost factory (defaulting to the scalar <see cref="Cost"/> factory).
    /// </summary>
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "AbstractRelOptPlanner(RelOptCostFactory, Context)")]
    protected AbstractPlanner(ICostFactory? costFactory = null)
    {
        _costFactory = costFactory ?? Cost.Factory;
        AddTraitDef(ConventionTraitDef.Instance);
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "getRelTraitDefs()")]
    public IReadOnlyList<TraitDef> TraitDefs => _traitDefs;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "getCostFactory()")]
    public ICostFactory CostFactory => _costFactory;

    /// <summary>
    /// The rules registered with this planner.
    /// </summary>
    protected IReadOnlyList<Rule> Rules => _rules;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "addRelTraitDef(RelTraitDef)")]
    public void AddTraitDef(TraitDef def)
    {
        if (_emptyTraitSet is not null)
            throw new InvalidOperationException("Trait dimensions must be registered before the empty trait set is used.");

        if (!_traitDefs.Contains(def))
            _traitDefs.Add(def);
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "emptyTraitSet()")]
    public TraitSet EmptyTraitSet
    {
        get
        {
            if (_emptyTraitSet is null)
            {
                var set = TraitSet.CreateEmpty();
                foreach (var def in _traitDefs)
                    set = set.Plus(def.Default);

                _emptyTraitSet = set;
            }

            return _emptyTraitSet;
        }
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "addRule(RelOptRule)")]
    public virtual bool AddRule(Rule rule)
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
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "removeRule(RelOptRule)")]
    public virtual bool RemoveRule(Rule rule)
    {
        if (!_mapDescToRule.Remove(rule.Description))
            return false;

        _rules.Remove(rule);
        return true;
    }

    /// <summary>
    /// The rule registered under the given description, or <c>null</c> if none.
    /// </summary>
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "getRuleByDescription(String)")]
    protected Rule? GetRuleByDescription(string description)
    {
        return _mapDescToRule.GetValueOrDefault(description);
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "prune(RelNode)")]
    public virtual void Prune(INode node)
    {
        // The base planner does not model pruning; the cost-based planner overrides this.
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "clear()")]
    public virtual void Clear()
    {
        // The base planner has no search state to reset; concrete planners override to clear theirs.
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "clearRelTraitDefs()")]
    public void ClearTraitDefs()
    {
        _traitDefs.Clear();
        _emptyTraitSet = null;
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "setRuleDescExclusionFilter(Pattern)")]
    public void SetRuleDescExclusionFilter(Regex? exclusionFilter)
    {
        _ruleDescExclusionFilter = exclusionFilter;
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "isRuleExcluded(RelOptRule)")]
    public bool IsRuleExcluded(Rule rule)
    {
        return _ruleDescExclusionFilter is not null && _ruleDescExclusionFilter.IsMatch(rule.Description);
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "addListener(RelOptListener)")]
    public void AddListener(IPlannerListener listener)
    {
        _listeners.Add(listener);
    }

    /// <summary>
    /// Whether any listener is attached. A planner may skip bookkeeping that only a listener observes.
    /// </summary>
    protected internal bool HasListeners => _listeners.Count > 0;

    /// <summary>
    /// Notifies listeners that a node has joined an equivalence class (optionally identified by
    /// <paramref name="equivalenceClass"/>, with <paramref name="isPhysical"/> flagging a physical node).
    /// </summary>
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "notifyEquivalence(RelNode, Object, boolean)")]
    protected internal void FireNodeEquivalenceFound(INode node, object? equivalenceClass = null, bool isPhysical = false)
    {
        foreach (var listener in _listeners)
            listener.NodeEquivalenceFound(new IPlannerListener.NodeEquivalenceEvent(this, node, equivalenceClass, isPhysical));
    }

    /// <summary>
    /// Notifies listeners that a rule is being attempted (before and after).
    /// </summary>
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "fireRule(RelOptRuleCall)")]
    protected internal void FireRuleAttempted(RuleCall call, bool before)
    {
        foreach (var listener in _listeners)
            listener.RuleAttempted(new IPlannerListener.RuleAttemptedEvent(this, call.Node(0), call, before));
    }

    /// <summary>
    /// Notifies listeners that a rule produced an equivalent.
    /// </summary>
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "notifyTransformation(RelOptRuleCall, RelNode, boolean)")]
    protected internal void FireRuleProductionSucceeded(RuleCall call, INode produced)
    {
        foreach (var listener in _listeners)
            listener.RuleProductionSucceeded(new IPlannerListener.RuleProductionEvent(this, produced, call, false));
    }

    /// <summary>
    /// Notifies listeners that a node has been discarded.
    /// </summary>
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "notifyDiscard(RelNode)")]
    protected internal void FireNodeDiscarded(INode node)
    {
        foreach (var listener in _listeners)
            listener.NodeDiscarded(new IPlannerListener.NodeDiscardedEvent(this, node));
    }

    /// <summary>
    /// Notifies listeners that a node has been chosen for the final plan (a null node signals the plan
    /// is complete).
    /// </summary>
    [Provenance("org.apache.calcite.plan.AbstractRelOptPlanner", "notifyChosen(RelNode)")]
    protected internal void FireNodeChosen(INode? node)
    {
        foreach (var listener in _listeners)
            listener.NodeChosen(new IPlannerListener.NodeChosenEvent(this, node));
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "setRoot(RelNode)")]
    public abstract void SetRoot(INode node);

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "getRoot()")]
    public abstract INode? Root { get; }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "changeTraits(RelNode, RelTraitSet)")]
    public abstract INode ChangeTraits(INode node, TraitSet toTraits);

    /// <inheritdoc />
    public abstract INode Convert(INode node, TraitSet toTraits);

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "findBestExp()")]
    public abstract INode FindBestPlan();

}
