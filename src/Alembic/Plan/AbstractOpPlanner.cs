using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

using Alembic.Algebra;
using Alembic.Algebra.Metadata;
using Alembic.Plan.Volcano;
using Alembic.Util;

namespace Alembic.Plan;

/// <summary>
/// Common machinery shared by planners — the registered trait dimensions, the empty trait set built
/// from them, the cost factory, and the rule registry. Concrete planners add the search itself
/// (<see cref="SetRoot"/> / <see cref="FindBestPlan"/>).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner")]
public abstract class AbstractOpPlanner : IOpPlanner
{

    readonly List<OpRule> _rules = new List<OpRule>();
    readonly Dictionary<string, OpRule> _mapDescToRule = new Dictionary<string, OpRule>();
    IPlannerListener? _listener;
    readonly IOpCostFactory _costFactory;
    Regex? _ruleDescExclusionFilter;

    /// <summary>
    /// The op types the planner has seen, tracked so rules that cannot match any registered op type can
    /// be skipped.
    /// </summary>
    protected readonly HashSet<Type> _classes = new HashSet<Type>();
    readonly HashSet<IConvention> _conventions = new HashSet<IConvention>();

    /// <summary>
    /// Initializes the planner with a cost factory (defaulting to the scalar <see cref="OpCost"/>
    /// factory). The trait-dimension registry lives on the cost-based planner — the base keeps no-op
    /// versions of the registry members, as Calcite's <c>AbstractRelOptPlanner</c> does.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "AbstractRelOptPlanner(RelOptCostFactory, Context)")]
    protected AbstractOpPlanner(IOpCostFactory costFactory, IContext? context = null)
    {
        _costFactory = costFactory ?? throw new ArgumentNullException(nameof(costFactory));
        Context = context ?? Contexts.Empty();
        CancellationToken = Context.MaybeUnwrap<CancellationToken>();

        // Add the abstract op classes. No op is ever registered with these types, but some operands may
        // match them.
        _classes.Add(typeof(IOp));
        _classes.Add(typeof(OpSubset));
    }

    /// <summary>
    /// The optional configuration this planner was created with. A caller passes config a planner may
    /// recognise (e.g. a <see cref="CancellationToken"/>) via the constructor's context.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "getContext()")]
    public IContext Context { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "getRelTraitDefs()")]
    public virtual IReadOnlyList<OpTraitDef> TraitDefs => Array.Empty<OpTraitDef>();

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "getCostFactory()")]
    public IOpCostFactory CostFactory => _costFactory;

    /// <summary>
    /// The token by which a caller cooperatively cancels a running plan (e.g. to impose a timeout).
    /// Unwrapped from the planner's <see cref="Context"/> if it carries one, otherwise
    /// <see cref="CancellationToken.None"/>. (The .NET idiomatic stand-in for Calcite's <c>CancelFlag</c>,
    /// which it likewise unwraps from the context; unlike that, a cancelled token cannot be un-cancelled.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "cancelFlag")]
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Throws if cancellation has been requested. Called at each rule firing so a plan can be aborted.
    /// The base throws <see cref="OperationCanceledException"/>; the cost-based planner overrides it to
    /// throw <see cref="Volcano.VolcanoTimeoutException"/>, which its drivers catch.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "checkCancel()")]
    public virtual void CheckCancel()
    {
        if (CancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();
    }

    /// <summary>
    /// The rules registered with this planner.
    /// </summary>
    protected IReadOnlyList<OpRule> Rules => _rules;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "addRelTraitDef(RelTraitDef)")]
    public virtual bool AddTraitDef(OpTraitDef def)
    {
        // The base planner keeps no trait-dimension registry (Calcite's base returns false). The
        // cost-based planner overrides this.
        return false;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "registerClass(RelNode)")]
    public void RegisterClass(IOp op)
    {
        var clazz = op.GetType();
        if (_classes.Add(clazz))
            OnNewClass(op);

        var convention = op.Convention;
        if (convention is not null && _conventions.Add(convention))
            convention.Register(this);
    }

    /// <summary>
    /// Called by <see cref="RegisterClass"/> the first time an op of a given concrete class is seen. The
    /// base lets the op register any class-specific rules (a no-op by default); the cost-based planner
    /// overrides it to index the class against the rules.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "onNewClass(RelNode)")]
    protected virtual void OnNewClass(IOp op)
    {
        op.Register(this);
    }

    /// <inheritdoc />
    public abstract bool IsRegistered(IOp op);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "onCopy(RelNode, RelNode)")]
    public virtual void OnCopy(IOp op, IOp newOp)
    {
        // do nothing
    }

    /// <summary>
    /// Returns the sub-classes of op (among the classes seen so far) for the given matched class.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "subClasses(Class)")]
    public IEnumerable<Type> SubClasses(Type clazz)
    {
        foreach (var c in _classes)
        {
            // OpSubset must be exact type, not subclass
            if (c == typeof(OpSubset))
            {
                if (c == clazz)
                    yield return c;
            }
            else if (clazz.IsAssignableFrom(c))
            {
                yield return c;
            }
        }
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "emptyTraitSet()")]
    public virtual OpTraitSet EmptyTraitSet => OpTraitSet.CreateEmpty();

    /// <summary>
    /// The cumulative cost of <paramref name="op"/>, via the metadata query, or <c>null</c> if it cannot
    /// be determined. The cost-based planner overrides this with its own walk.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "getCost(RelNode, RelMetadataQuery)")]
    public virtual IOpCost? GetCost(IOp op, OpMetadataQuery mq) => mq.GetCumulativeCost(op);

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

            throw new InvalidOperationException("Rule's description should be unique; existing rule=" + existing + "; new rule=" + rule);
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
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "clearRelTraitDefs()")]
    public virtual void ClearTraitDefs()
    {
        // No-op on the base; the cost-based planner overrides this.
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

        // = Calcite's ruleDescExclusionFilter.matcher(rule.toString()).matches(). Java's matches() makes
        // the pattern consume the *whole* description (anchored both ends, backtracking across alternation);
        // .NET's Match is leftmost-substring, so anchor the pattern end to end. A leftmost-match + length
        // check would disagree with matches() when an alternative matches only a prefix (e.g. "a|abc" on
        // "abc": matches() succeeds via the second alternative, but the leftmost match is the prefix "a").
        return Regex.IsMatch(rule.ToString(), @"\A(?:" + _ruleDescExclusionFilter + @")\z", _ruleDescExclusionFilter.Options);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "addListener(RelOptListener)")]
    public void AddListener(IPlannerListener newListener)
    {
        // The planner's listener is always a multicast: the first registration creates it, and every
        // listener (even a single one) is added to it.
        if (_listener is null)
            _listener = new MulticastPlannerListener();

        ((MulticastPlannerListener)_listener).AddListener(newListener);
    }

    /// <summary>
    /// The attached listener (a <see cref="MulticastPlannerListener"/> when more than one was added), or
    /// <c>null</c>. A planner may skip bookkeeping that only a listener observes.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "getListener()")]
    protected internal IPlannerListener? Listener => _listener;

    /// <summary>
    /// Whether any listener is attached.
    /// </summary>
    protected internal bool HasListeners => _listener is not null;

    /// <summary>
    /// Takes a rule referenced by a rule call: checks for cancellation, verifies the rule still matches,
    /// applies the exclusion filter, notifies listeners, and invokes the rule's
    /// <see cref="OpRule.OnMatch"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "fireRule(RelOptRuleCall)")]
    protected internal void FireRule(OpRuleCall ruleCall)
    {
        CheckCancel();

        Debug.Assert(ruleCall.Rule.Matches(ruleCall));
        if (IsRuleExcluded(ruleCall.Rule))
            return;

        if (ruleCall.IsRuleExcluded())
            return;

        FireRuleAttempted(ruleCall, before: true);
        ruleCall.Rule.OnMatch(ruleCall);
        FireRuleAttempted(ruleCall, before: false);
    }

    /// <summary>
    /// Notifies listeners that a rule is being attempted (before and after). Shared by <see cref="FireRule"/>
    /// and <see cref="Volcano.VolcanoRuleCall.OnMatch"/>, which both fire this event inline in Calcite.
    /// </summary>
    protected internal void FireRuleAttempted(OpRuleCall call, bool before)
    {
        _listener?.RuleAttempted(new IPlannerListener.RuleAttemptedEvent(this, call.Op(0), call, before));
    }

    /// <summary>
    /// Notifies listeners that a rule produced an equivalent.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "notifyTransformation(RelOptRuleCall, RelNode, boolean)")]
    protected internal void FireRuleProductionSucceeded(OpRuleCall call, IOp produced, bool before)
    {
        _listener?.RuleProductionSucceeded(new IPlannerListener.RuleProductionEvent(this, produced, call, before));
    }

    /// <summary>
    /// Notifies listeners that an op has been discarded.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "notifyDiscard(RelNode)")]
    protected internal void FireOpDiscarded(IOp op)
    {
        _listener?.OpDiscarded(new IPlannerListener.OpDiscardedEvent(this, op));
    }

    /// <summary>
    /// Notifies listeners that an op has been chosen for the final plan (a null op signals the plan
    /// is complete).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.AbstractRelOptPlanner", "notifyChosen(RelNode)")]
    protected internal void FireOpChosen(IOp? op)
    {
        _listener?.OpChosen(new IPlannerListener.OpChosenEvent(this, op));
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
