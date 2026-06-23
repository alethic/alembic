using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Alembic.Algebra;
using Alembic.Algebra.Convert;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// A cost-based planner. It registers ops into equivalence <see cref="OpSet"/>s, fires rules to
/// discover equivalent forms, costs every form, and extracts the cheapest plan.
/// </summary>
/// <remarks>
/// Registration drives rule matching; matches are deferred to a <see cref="RuleQueue"/> and applied by
/// a <see cref="IRuleDriver"/>, and each subset remembers its cheapest member. Two drivers are provided:
/// the exhaustive bottom-up <see cref="IterativeRuleDriver"/> (the default) and the top-down
/// <see cref="TopDownRuleDriver"/> (Cascades), selected with <see cref="SetTopDownOpt"/>.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner")]
public sealed class VolcanoPlanner : AbstractOpPlanner
{

    readonly List<OpSet> _allSets = new List<OpSet>();
    readonly Dictionary<IOpDigest, IOp> _digestToOp = new Dictionary<IOpDigest, IOp>();
    readonly Dictionary<IOp, OpSubset> _opToSubset = new Dictionary<IOp, OpSubset>(ReferenceEqualityComparer.Instance);
    readonly Dictionary<Type, List<OpRuleOperand>> _classOperands = new Dictionary<Type, List<OpRuleOperand>>();
    readonly HashSet<Type> _classes = new HashSet<Type>();
    readonly HashSet<IOp> _prunedOps = new HashSet<IOp>(ReferenceEqualityComparer.Instance);

    IRuleDriver _ruleDriver;
    OpSubset? _root;
    OpCluster? _cluster;
    bool _topDownOpt;
    bool _locked;
    int _nextSetId;

    /// <summary>
    /// Creates a planner with an optional cost factory (defaulting to <see cref="VolcanoCost"/>).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "VolcanoPlanner(RelOptCostFactory, Context)")]
    public VolcanoPlanner(IOpCostFactory? costFactory = null)
        : base(costFactory ?? VolcanoCost.Factory)
    {
        _ruleDriver = new IterativeRuleDriver(this);
        AddRule(new ExpandConversionRule());
    }

    /// <summary>
    /// The driver that applies queued rule matches.
    /// </summary>
    internal IRuleDriver RuleDriver => _ruleDriver;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "getRoot()")]
    public override IOp? Root => _root;

    /// <summary>
    /// Whether the planner uses the top-down (Cascades) search rather than the default bottom-up search.
    /// </summary>
    public bool TopDownOpt => _topDownOpt;

    /// <summary>
    /// Chooses between the iterative (bottom-up) and top-down (Cascades) search strategies.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "setTopDownOpt(boolean)")]
    public void SetTopDownOpt(bool value)
    {
        _topDownOpt = value;
        _ruleDriver = value ? new TopDownRuleDriver(this) : new IterativeRuleDriver(this);
    }

    /// <summary>
    /// Locks or unlocks the planner. A locked planner accepts no new rules: <see cref="AddRule"/> does
    /// nothing and returns <c>false</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "setLocked(boolean)")]
    public void SetLocked(bool locked) => _locked = locked;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "addRule(RelOptRule)")]
    public override bool AddRule(OpRule rule)
    {
        if (_locked)
            return false;

        if (!base.AddRule(rule))
            return false;

        // Each of the rule's operands is an entry point for a match. Index every operand against the
        // concrete op classes already seen that could bind it — but a transformation rule is never
        // indexed against physical op classes, since it rewrites within a convention rather than
        // implementing one.
        bool isTransformationRule = rule is ITransformationRule;
        foreach (var operand in rule.Operands)
            foreach (var subClass in SubClasses(operand.MatchedClass))
            {
                if (isTransformationRule && typeof(IPhysicalOp).IsAssignableFrom(subClass))
                    continue;

                OperandsFor(subClass).Add(operand);
            }

        // A converter rule registers itself with the trait dimension it converts, building the
        // conversion graph that the abstract converters expand against.
        if (rule is ConverterRule converterRule)
        {
            var def = converterRule.Source.TraitDef;
            if (TraitDefs.Contains(def))
                def.RegisterConverterRule(this, converterRule);
        }

        return true;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "removeRule(RelOptRule)")]
    public override bool RemoveRule(OpRule rule)
    {
        if (!base.RemoveRule(rule))
            return false;

        foreach (var operands in _classOperands.Values)
            operands.RemoveAll(operand => ReferenceEquals(operand.Rule, rule));

        if (rule is ConverterRule converterRule)
        {
            var def = converterRule.Source.TraitDef;
            if (TraitDefs.Contains(def))
                def.DeregisterConverterRule(this, converterRule);
        }

        return true;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "clear()")]
    public override void Clear()
    {
        base.Clear();

        foreach (var rule in new List<OpRule>(Rules))
            RemoveRule(rule);

        _classOperands.Clear();
        _classes.Clear();
        _allSets.Clear();
        _digestToOp.Clear();
        _opToSubset.Clear();
        _prunedOps.Clear();
        _ruleDriver.Clear();
        _root = null;
        _cluster = null;
        _nextSetId = 0;
    }

    List<OpRuleOperand> OperandsFor(Type clazz)
    {
        if (!_classOperands.TryGetValue(clazz, out var operands))
        {
            operands = new List<OpRuleOperand>();
            _classOperands[clazz] = operands;
        }

        return operands;
    }

    /// <summary>
    /// The concrete op classes seen so far that are assignable to <paramref name="matchedClass"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "subClasses(Class)")]
    IEnumerable<Type> SubClasses(Type matchedClass)
    {
        foreach (var clazz in _classes)
            if (matchedClass.IsAssignableFrom(clazz))
                yield return clazz;
    }

    /// <summary>
    /// Records an op's concrete class the first time it is seen, so that instances of it match the
    /// operands of every rule registered so far (and any registered later, via <see cref="AddRule"/>).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "onNewClass(RelNode)")]
    void OnNewClass(IOp op)
    {
        var clazz = op.GetType();
        if (!_classes.Add(clazz))
            return;

        bool isPhysical = typeof(IPhysicalOp).IsAssignableFrom(clazz);
        foreach (var rule in Rules)
        {
            // A transformation rule never matches a physical op.
            if (isPhysical && rule is ITransformationRule)
                continue;

            foreach (var operand in rule.Operands)
                if (operand.MatchedClass.IsAssignableFrom(clazz))
                    OperandsFor(clazz).Add(operand);
        }
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "setRoot(RelNode)")]
    public override void SetRoot(IOp op)
    {
        _cluster ??= op.Cluster;
        _root = EnsureRegistered(op, null);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "changeTraits(RelNode, RelTraitSet)")]
    public override IOp ChangeTraits(IOp op, OpTraitSet toTraits)
    {
        var subset = EnsureRegistered(op, null);
        return EquivRoot(subset.Set).GetOrCreateSubset(toTraits, required: true);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "findBestExp()")]
    public override IOp FindBestPlan()
    {
        if (_root is null)
            throw new InvalidOperationException("No root has been set.");

        EnsureRootConverters();
        _ruleDriver.Drive();

        return _root.BuildCheapestPlan(this);
    }

    /// <summary>
    /// Registers an op as a member of the same set as <paramref name="equivalent"/> (or a fresh set
    /// when that is <c>null</c>), returning the subset it lands in.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "register(RelNode, RelNode)")]
    internal OpSubset Register(IOp op, IOp? equivalent)
    {
        OpSet? set = null;
        if (equivalent is not null)
        {
            var equivSubset = EnsureRegistered(equivalent, null);
            set = EquivRoot(equivSubset.Set);
        }

        return RegisterImpl(op, set);
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "ensureRegistered(RelNode, RelNode)")]
    internal OpSubset EnsureRegistered(IOp op, IOp? equivalent)
    {
        if (op is OpSubset subset)
            return subset;

        if (_opToSubset.TryGetValue(op, out var existing))
        {
            // Already registered. If a known equivalent lives in a different set, the two sets are
            // equivalent and must be merged.
            if (equivalent is not null)
            {
                var equivSubset = GetSubsetNonNull(equivalent);
                if (existing.Set != equivSubset.Set)
                    Merge(equivSubset.Set, existing.Set);
            }

            return Canonize(existing);
        }

        return Register(op, equivalent);
    }

    /// <summary>
    /// Find the new root subset in case the root is merged with another subset.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "canonize()")]
    internal void Canonize()
    {
        _root = Canonize(_root!);
    }

    /// <summary>
    /// If a subset has one or more equivalent subsets (owing to a set having merged with another),
    /// returns the subset which is the leader of the equivalence class.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "canonize(RelSubset)")]
    static OpSubset Canonize(OpSubset subset)
    {
        if (subset.Set.EquivalentSet is null)
            return subset;

        return EquivRoot(subset.Set).GetOrCreateSubset(subset.Traits, subset.IsRequired);
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "registerImpl(RelNode, RelSet)")]
    OpSubset RegisterImpl(IOp op, OpSet? set)
    {
        if (op is OpSubset subset)
            return RegisterSubset(set, subset);

        // An op must implement the interface its convention requires; a converter is exempt, since it
        // exists to bridge conventions.
        if (op is not IConverter && !op.Convention.Interface.IsInstanceOfType(op))
            throw new InvalidOperationException($"Op '{op.GetType().Name}' is not an instance of the '{op.Convention.Interface.Name}' interface required by convention '{op.Convention}'.");

        // Make sure the children are registered first, replacing each with its subset.
        op = OnRegister(op);

        // If an equivalent expression already exists, join its set (and carry over its pruned state).
        if (_digestToOp.TryGetValue(op.GetDigest(), out var equiv))
        {
            CheckPruned(equiv, op);
            var equivSubset = _opToSubset[equiv];
            return RegisterSubset(set, equivSubset);
        }

        // Converters are in the same set as their children.
        if (op is IConverter converter)
        {
            var input = converter.Input;
            var childSet = EquivRoot(((OpSubset)input).Set);
            if (set is not null
                && set != childSet
                && set.EquivalentSet is null)
            {
                Merge(set, childSet);

                // During the mergers, the child set may have changed, and since we're not registered
                // yet, we won't have been informed. So check whether we are now equivalent to an
                // existing expression.
                if (FixUpInputs(op))
                {
                    var digest = op.GetDigest();
                    var equivOp = _digestToOp.GetValueOrDefault(digest);
                    if (equivOp != op && equivOp is not null)
                    {
                        // make sure this bad op didn't get into the set in any way (fixUpInputs will do
                        // this but it doesn't know if it should so it does it anyway)
                        set.ObliterateOp(op);

                        // There is already an equivalent expression. Use that one, and forget about this one.
                        return GetSubsetNonNull(equivOp);
                    }
                }
            }
            else
            {
                set = childSet;
            }
        }

        if (set is null)
        {
            set = new OpSet(_nextSetId++, CostFactory, _cluster ??= op.Cluster);
            _allSets.Add(set);
        }

        set = EquivRoot(set);

        var added = AddOpToSet(op, set);
        _digestToOp[op.GetDigest()] = op;

        foreach (var child in op.Children)
            ((OpSubset)child).Set.Parents.Add(op);

        OnNewClass(op);
        FireRules(op);

        _ruleDriver.OnProduce(op, added);

        return added;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "registerSubset(RelSet, RelSubset)")]
    OpSubset RegisterSubset(OpSet? set, OpSubset subset)
    {
        if (set != subset.Set
            && set is not null
            && set.EquivalentSet is null)
            Merge(set, subset.Set);

        return Canonize(subset);
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "onRegister(RelOptPlanner)")]
    IOp OnRegister(IOp op)
    {
        if (op.Children.IsEmpty)
            return op;

        // An op requires its inputs in its own convention — a consumer asks for its inputs in a
        // particular convention — except a converter, which exists precisely to bridge conventions.
        // This propagates a lowering down the tree; the root, having no consumer, is handled separately
        // by the abstract converters added in EnsureRootConverters.
        var bridge = op is IConverter;
        var convention = op.Traits.Convention;

        var children = ImmutableArray.CreateBuilder<IOp>(op.Children.Length);
        foreach (var child in op.Children)
        {
            var childSubset = EnsureRegistered(child, null);
            if (!bridge && !childSubset.Traits.Convention.Equals(convention))
                childSubset = childSubset.Set.GetOrCreateSubset(childSubset.Traits.Replace(ConventionTraitDef.Instance, convention));

            children.Add(childSubset);
        }

        return op.Copy(op.Traits, children.MoveToImmutable());
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "addRelToSet(RelNode, RelSet)")]
    OpSubset AddOpToSet(IOp op, OpSet set)
    {
        var subset = set.Add(op);
        _opToSubset[op] = subset;
        PropagateCostImprovements(op);
        return subset;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "propagateCostImprovements(RelNode)")]
    internal void PropagateCostImprovements(IOp op)
    {
        // A best-first worklist: ops whose cost may have improved, processed cheapest-first. The cost
        // for each op lives in the map (read from there, not the heap); the heap only orders the work.
        var propagateOps = new Dictionary<IOp, IOpCost>(ReferenceEqualityComparer.Instance);
        var propagateHeap = new PriorityQueue<IOp, IOpCost>(Comparer<IOpCost>.Create(
            (a, b) => a.IsLessThan(b) ? -1 : b.IsLessThan(a) ? 1 : 0));

        propagateOps[op] = GetCost(op);
        propagateHeap.Enqueue(op, propagateOps[op]);

        while (propagateHeap.TryDequeue(out var current, out _))
        {
            var cost = propagateOps[current];

            foreach (var subset in GetSubsetNonNull(current).Set.Subsets)
            {
                if (!current.Traits.Satisfies(subset.Traits))
                    continue;

                // Not the current best and not cheaper than it → no change.
                if (!ReferenceEquals(current, subset.Best) && !cost.IsLessThan(subset.BestCost))
                    continue;

                // Already the best at the same cost → nothing to do.
                if (ReferenceEquals(current, subset.Best) && CostEquals(cost, subset.BestCost))
                    continue;

                subset.BestCost = cost;
                subset.Best = current;

                foreach (var parent in subset.GetParents())
                {
                    var newCost = GetCost(parent);
                    if (!propagateOps.TryGetValue(parent, out var existingCost) || newCost.IsLessThan(existingCost))
                    {
                        propagateOps[parent] = newCost;

                        // The reference removes the stale heap entry before re-offering; .NET's PriorityQueue
                        // has no remove, so the stale (higher-cost) entry stays and is harmlessly skipped when
                        // polled — the cost is read from the map, so re-polling an op is a no-op.
                        propagateHeap.Enqueue(parent, newCost);
                    }
                }
            }
        }
    }

    static bool CostEquals(IOpCost a, IOpCost b) => !a.IsLessThan(b) && !b.IsLessThan(a);

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "getCost(RelNode, RelMetadataQuery)")]
    IOpCost GetCost(IOp op)
    {
        if (op is OpSubset subset)
            return subset.BestCost;

        var cost = op.ComputeSelfCost(this);
        foreach (var child in op.Children)
            cost = cost.Plus(GetCost(child));

        return cost;
    }

    /// <summary>
    /// Fires every rule matched by a just-registered op. The dispatch table yields only the operands
    /// that could bind an op of this class; each such operand seeds a match that solves outward.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "fireRules(RelNode)")]
    internal void FireRules(IOp op)
    {
        if (!_classOperands.TryGetValue(op.GetType(), out var operands))
            return;

        foreach (var operand in operands.ToArray())
            if (operand.Matches(op))
                new DeferringRuleCall(this, operand).Match(op);
    }

    /// <summary>
    /// The subset a registered op belongs to. (The traversal helpers <c>GetParentRels</c> / <c>GetRels</c>
    /// / <c>Contains</c> live on <see cref="OpSubset"/>.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "getSubsetNonNull(RelNode)")]
    internal OpSubset GetSubsetNonNull(IOp op) => _opToSubset[op];

    /// <summary>
    /// The subset an op belongs to, or <c>null</c> if it is not registered (or has been removed).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "getSubset(RelNode)")]
    internal OpSubset? GetSubset(IOp op) => _opToSubset.GetValueOrDefault(op);

    /// <summary>
    /// Prunes an op: marks it as having zero importance, so it is not added to a set on registration and
    /// no rule fires on a match that touches it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "prune(RelNode)")]
    public override void Prune(IOp op) => _prunedOps.Add(op);

    /// <summary>
    /// Whether <paramref name="op"/> has been pruned.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "prunedOps")]
    internal bool IsPruned(IOp op) => _prunedOps.Contains(op);

    /// <summary>
    /// Propagates pruning across a newly discovered equivalence: if <paramref name="duplicate"/> is
    /// pruned, then <paramref name="op"/> (equivalent to it) is pruned too.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "checkPruned(RelNode, RelNode)")]
    void CheckPruned(IOp op, IOp duplicate)
    {
        if (_prunedOps.Contains(duplicate))
            _prunedOps.Add(op);
    }

    // ~ Top-down (Cascades) search support -------------------------------------

    /// <summary>
    /// The root subset (typed), or <c>null</c> if no root has been set.
    /// </summary>
    internal OpSubset? RootSubset => _root;

    /// <summary>
    /// The convention the root is requested in. An op in any other (non-physical) convention is logical.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "rootConvention")]
    internal IConvention? RootConvention => _root?.Traits.Convention;

    /// <summary>
    /// A zero cost from the active factory.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "zeroCost")]
    internal IOpCost ZeroCost => CostFactory.MakeZeroCost();

    /// <summary>
    /// An infinite cost from the active factory.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "infCost")]
    internal IOpCost InfiniteCost => CostFactory.MakeInfiniteCost();

    /// <summary>
    /// Whether an op is logical: not physical, and not already in the requested root convention.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "isLogical(RelNode)")]
    internal bool IsLogical(IOp op)
    {
        return op is not IPhysicalOp && !op.Convention.Equals(RootConvention);
    }

    /// <summary>
    /// Whether a match is for a transformation rule.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "isTransformationRule(VolcanoRuleCall)")]
    internal bool IsTransformationRule(VolcanoRuleMatch match) => match.Rule is ITransformationRule;

    /// <summary>
    /// Whether a match is for a substitution rule. Alembic has no substitution rules.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "isSubstituteRule(VolcanoRuleCall)")]
    internal bool IsSubstituteRule(VolcanoRuleMatch match) => false;

    /// <summary>
    /// Whether an op has been registered.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "isRegistered(RelNode)")]
    internal bool IsRegistered(IOp op) => _opToSubset.ContainsKey(op);

    /// <summary>
    /// The live set a registered op belongs to.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "getSet(RelNode)")]
    internal OpSet GetSet(IOp op) => EquivRoot(_opToSubset[op].Set);

    /// <summary>
    /// The lower bound of an op's cost. Pending the metadata subsystem this is the trivial zero bound,
    /// so the top-down search keeps its branch-and-bound structure but performs no lower-bound pruning.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "getLowerBound(RelNode)")]
    internal IOpCost GetLowerBound(IOp op) => ZeroCost;

    /// <summary>
    /// The upper bound to allow an op's inputs, given the op's own upper bound: the bound minus the
    /// op's self cost.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "upperBoundForInputs(RelNode, RelOptCost)")]
    internal IOpCost UpperBoundForInputs(IOp op, IOpCost upperBound)
    {
        if (!upperBound.IsInfinite)
        {
            var selfCost = op.ComputeSelfCost(this);
            if (!selfCost.IsInfinite)
                return upperBound.Minus(selfCost);
        }

        return upperBound;
    }

    /// <summary>
    /// Puts an <see cref="AbstractConverter"/> into the root's set for each delivered subset whose
    /// traits differ from the requested root traits. The root is the only place explicit converters are
    /// needed — everywhere else a parent has already asked for its inputs' convention (see
    /// <see cref="OnRegister"/>). <see cref="ExpandConversionRule"/> then turns each one into a real
    /// conversion.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "ensureRootConverters()")]
    void EnsureRootConverters()
    {
        if (_root is null)
            return;

        var set = EquivRoot(_root.Set);
        foreach (var subset in set.Subsets.ToArray())
        {
            if (subset.Traits.Equals(_root.Traits))
                continue;

            Register(new AbstractConverter(_root.Traits, subset), _root);
        }
    }

    /// <summary>
    /// Converts a subset to the given traits, finding a (possibly multi-step) chain of converter rules
    /// and trait-dimension conversion hooks and applying it. Returns the target subset once it has a
    /// member, or <c>null</c> if no chain reaches the traits.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "changeTraitsUsingConverters(RelNode, RelTraitSet)")]
    internal IOp? ChangeTraitsUsingConverters(IOp op, OpTraitSet toTraits)
    {
        var subset = (OpSubset)op;
        if (subset.Traits.Equals(toTraits))
            return subset;

        var path = FindConversionPath(subset.Traits, toTraits);
        if (path is null)
            return null;

        var current = subset;
        foreach (var step in path)
        {
            foreach (var member in EquivRoot(current.Set).Ops.ToArray())
            {
                if (!member.Traits.Satisfies(current.Traits))
                    continue;

                var converted = step.Convert(member);
                if (converted is not null)
                    Register(converted, member);
            }

            var next = EquivRoot(current.Set).GetSubset(step.Result);
            if (next is null)
                return null;

            current = next;
        }

        return current.Best is not null ? current : null;
    }

    List<ConversionStep>? FindConversionPath(OpTraitSet from, OpTraitSet to)
    {
        var visited = new HashSet<OpTraitSet> { from };
        var cameFrom = new Dictionary<OpTraitSet, (OpTraitSet Previous, ConversionStep Step)>();
        var queue = new Queue<OpTraitSet>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var (next, step) in ConversionEdges(current, to))
            {
                if (!visited.Add(next))
                    continue;

                cameFrom[next] = (current, step);
                if (next.Equals(to))
                    return Reconstruct(cameFrom, from, to);

                queue.Enqueue(next);
            }
        }

        return null;
    }

    IEnumerable<(OpTraitSet Next, ConversionStep Step)> ConversionEdges(OpTraitSet current, OpTraitSet to)
    {
        // A registered converter rule converts the dimension named by its Source/Target.
        foreach (var rule in Rules)
        {
            if (rule is not ConverterRule converter)
                continue;

            if (!current.Get(converter.Source.TraitDef).Equals(converter.Source))
                continue;

            var next = current.Plus(converter.Target);
            if (!next.Equals(current))
                yield return (next, new ConversionStep(next, member => converter.Convert(member)));
        }

        // A dimension may convert itself toward its goal value via its trait-def hook.
        foreach (var def in TraitDefs)
        {
            var source = current.Get(def);
            var target = to.Get(def);
            if (source.Equals(target) || !def.CanConvert(this, source, target))
                continue;

            var next = current.Plus(target);
            var hookDef = def;
            if (!next.Equals(current))
                yield return (next, new ConversionStep(next, member => hookDef.Convert(this, member, target, true)));
        }
    }

    static List<ConversionStep> Reconstruct(Dictionary<OpTraitSet, (OpTraitSet Previous, ConversionStep Step)> cameFrom, OpTraitSet from, OpTraitSet to)
    {
        var steps = new List<ConversionStep>();
        var trait = to;
        while (!trait.Equals(from))
        {
            var (previous, step) = cameFrom[trait];
            steps.Add(step);
            trait = previous;
        }

        steps.Reverse();
        return steps;
    }

    sealed class ConversionStep
    {

        public ConversionStep(OpTraitSet result, Func<IOp, IOp?> convert)
        {
            Result = result;
            Convert = convert;
        }

        public OpTraitSet Result { get; }

        public Func<IOp, IOp?> Convert { get; }

    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "merge(RelSet, RelSet)")]
    void Merge(OpSet set1, OpSet set2)
    {
        set1 = EquivRoot(set1);
        set2 = EquivRoot(set2);
        if (set1 == set2)
            return;

        // Swap so we always merge the newer/smaller set into the older/larger one, or a child set into
        // its parent set.
        var childrenOf1 = set1.GetChildSets();
        var childrenOf2 = set2.GetChildSets();
        var set2IsParentOfSet1 = childrenOf2.Contains(set1);
        var set1IsParentOfSet2 = childrenOf1.Contains(set2);

        bool swap;
        if (set2IsParentOfSet1 && set1IsParentOfSet2)
            swap = IsSmaller(set1, set2);   // a 1-cycle: merge into the larger/older
        else if (set2IsParentOfSet1)
            swap = false;                   // set2 is a parent of set1: merge set2 into set1
        else if (set1IsParentOfSet2)
            swap = true;                    // set1 is a parent of set2: merge set1 into set2
        else
            swap = IsSmaller(set1, set2);   // unrelated: merge into the larger/older

        if (swap)
            (set1, set2) = (set2, set1);

        // The root's set must be read before the merge, since the merge re-points it.
        var rootSet = _root is not null ? EquivRoot(_root.Set) : null;

        set1.MergeWith(this, set2);

        // If the absorbed set held the root, the survivor's subset for the root traits is the new root.
        if (_root is not null && ReferenceEquals(set2, rootSet))
        {
            _root = set1.GetOrCreateSubset(_root.Traits, _root.IsRequired);
            EnsureRootConverters();
        }
    }

    /// <summary>
    /// Whether <paramref name="set1"/> is less popular (fewer parents), smaller (fewer ops), or younger
    /// (higher id) than <paramref name="set2"/> — in which case it is cheaper to merge it into the other.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "isSmaller(RelSet, RelSet)")]
    static bool IsSmaller(OpSet set1, OpSet set2)
    {
        if (set1.Parents.Count != set2.Parents.Count)
            return set1.Parents.Count < set2.Parents.Count;
        if (set1.Ops.Count != set2.Ops.Count)
            return set1.Ops.Count < set2.Ops.Count;

        return set1.Id > set2.Id;
    }

    /// <summary>
    /// Removes a set from the live registry (called by <see cref="OpSet.MergeWith"/> when a set is
    /// absorbed). The C# analog of the upstream package-private <c>allSets.remove</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "allSets")]
    internal void RemoveSet(OpSet set) => _allSets.Remove(set);

    /// <summary>
    /// Records the subset an op now belongs to (called by <see cref="OpSet.MergeWith"/> as it moves
    /// ops into the surviving set).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "mapRel2Subset")]
    internal void MapOpToSubset(IOp op, OpSubset subset) => _opToSubset[op] = subset;

    /// <summary>
    /// Re-points an op's child subsets at their live (merged) sets, replacing them in place; returns
    /// whether anything changed, recomputing the op's digest if so.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "fixUpInputs(RelNode)")]
    bool FixUpInputs(IOp op)
    {
        var inputs = op.Children;
        var newInputs = new List<IOp>(inputs.Length);
        int changeCount = 0;
        foreach (var input in inputs)
        {
            var subset = (OpSubset)input;
            OpSubset newSubset = Canonize(subset);
            newInputs.Add(newSubset);
            if (newSubset != subset)
            {
                if (subset.Set != newSubset.Set)
                {
                    subset.Set.Parents.Remove(op);
                    newSubset.Set.Parents.Add(op);
                }

                changeCount++;
            }
        }

        if (changeCount > 0)
        {
            _digestToOp.Remove(op.GetDigest());
            for (int i = 0; i < inputs.Length; i++)
                op.ReplaceInput(i, newInputs[i]);

            op.RecomputeDigest();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Re-computes the digest of an op. Since an op's digest contains the identifiers of its children,
    /// this is called when a child has been renamed — for example if the child's set merges with another.
    /// If the op then coincides with one already registered, that one is kept and this op is forgotten
    /// (its sets merged).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "rename(RelNode)")]
    internal void Rename(IOp op)
    {
        if (FixUpInputs(op))
        {
            var newDigest = op.GetDigest();
            _digestToOp.TryGetValue(newDigest, out var equiv);
            _digestToOp[newDigest] = op;
            if (equiv is not null)
            {
                // There's already an equivalent with the same name, and we just knocked it out. Put it
                // back, and forget about op.
                _digestToOp[newDigest] = equiv;
                CheckPruned(equiv, op);

                var equivOpSubset = GetSubsetNonNull(equiv);

                // Remove back-links from children.
                foreach (var input in op.Children)
                    ((OpSubset)input).Set.Parents.Remove(op);

                // Remove op from its subset. (This may leave the subset empty, but if so, that will be
                // dealt with when the sets get merged.)
                var subset = _opToSubset[op];
                _opToSubset[op] = equivOpSubset;
                bool existed = subset.Set.Ops.Remove(op);
                if (!existed)
                    throw new InvalidOperationException("op was not known to its set");

                var equivSubset = GetSubsetNonNull(equiv);
                foreach (var s in subset.Set.Subsets)
                {
                    if (ReferenceEquals(s.Best, op))
                    {
                        s.Best = equiv;
                        PropagateCostImprovements(equiv);
                    }
                }

                if (equivSubset != subset)
                    Merge(equivSubset.Set, subset.Set);
            }
        }
    }

    /// <summary>
    /// The live representative of a set: the set itself, or the set it was merged into (following the
    /// chain).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoPlanner", "equivRoot(RelSet)")]
    internal static OpSet EquivRoot(OpSet set)
    {
        while (set.EquivalentSet is not null)
            set = set.EquivalentSet;

        return set;
    }

}
