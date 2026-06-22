using System;
using System.Collections.Generic;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan;

/// <summary>
/// Common machinery shared by planners — the registered trait dimensions, the empty trait set built
/// from them, the cost factory, and the rule registry. Concrete planners add the search itself
/// (<see cref="SetRoot"/> / <see cref="FindBestPlan"/>).
/// </summary>
public abstract class AbstractPlanner : IPlanner
{

    readonly List<ITraitDef> _traitDefs = new List<ITraitDef>();
    readonly List<IRule> _rules = new List<IRule>();
    readonly List<IPlannerListener> _listeners = new List<IPlannerListener>();
    readonly ICostFactory _costFactory;
    TraitSet? _emptyTraitSet;

    /// <summary>
    /// Initializes the planner with the convention dimension registered (every plan has a convention)
    /// and a cost factory (defaulting to the scalar <see cref="Cost"/> factory).
    /// </summary>
    protected AbstractPlanner(ICostFactory? costFactory = null)
    {
        _costFactory = costFactory ?? Cost.Factory;
        AddTraitDef(ConventionTraitDef.Instance);
    }

    /// <inheritdoc />
    public IReadOnlyList<ITraitDef> TraitDefs => _traitDefs;

    /// <inheritdoc />
    public ICostFactory CostFactory => _costFactory;

    /// <summary>
    /// The rules registered with this planner.
    /// </summary>
    protected IReadOnlyList<IRule> Rules => _rules;

    /// <inheritdoc />
    public void AddTraitDef(ITraitDef def)
    {
        if (_emptyTraitSet is not null)
            throw new InvalidOperationException("Trait dimensions must be registered before the empty trait set is used.");

        if (!_traitDefs.Contains(def))
            _traitDefs.Add(def);
    }

    /// <inheritdoc />
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
    public void AddRule(IRule rule)
    {
        _rules.Add(rule);
    }

    /// <inheritdoc />
    public void AddListener(IPlannerListener listener)
    {
        _listeners.Add(listener);
    }

    /// <summary>
    /// Notifies listeners that a node has joined an equivalence class.
    /// </summary>
    protected internal void FireNodeEquivalenceFound(INode node)
    {
        foreach (var listener in _listeners)
            listener.NodeEquivalenceFound(new IPlannerListener.NodeEquivalenceEvent(this, node));
    }

    /// <summary>
    /// Notifies listeners that a rule is being attempted (before and after).
    /// </summary>
    protected internal void FireRuleAttempted(RuleCall call, bool before)
    {
        foreach (var listener in _listeners)
            listener.RuleAttempted(new IPlannerListener.RuleAttemptedEvent(this, call.Node(0), call, before));
    }

    /// <summary>
    /// Notifies listeners that a rule produced an equivalent.
    /// </summary>
    protected internal void FireRuleProductionSucceeded(RuleCall call, INode produced)
    {
        foreach (var listener in _listeners)
            listener.RuleProductionSucceeded(new IPlannerListener.RuleProductionEvent(this, produced, call, false));
    }

    /// <summary>
    /// Notifies listeners that a node has been discarded.
    /// </summary>
    protected internal void FireNodeDiscarded(INode node)
    {
        foreach (var listener in _listeners)
            listener.NodeDiscarded(new IPlannerListener.NodeDiscardedEvent(this, node));
    }

    /// <summary>
    /// Notifies listeners that a node has been chosen for the final plan (a null node signals the plan
    /// is complete).
    /// </summary>
    protected internal void FireNodeChosen(INode? node)
    {
        foreach (var listener in _listeners)
            listener.NodeChosen(new IPlannerListener.NodeChosenEvent(this, node));
    }

    /// <inheritdoc />
    public abstract void SetRoot(INode node);

    /// <inheritdoc />
    public abstract INode ChangeTraits(INode node, TraitSet toTraits);

    /// <inheritdoc />
    public abstract INode FindBestPlan();

}
