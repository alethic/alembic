using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Alembic.Algebra;
using Alembic.Algebra.Convert;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// A cost-based planner. It registers nodes into equivalence <see cref="NodeSet"/>s, fires rules to
/// discover equivalent forms, costs every form, and extracts the cheapest plan.
/// </summary>
/// <remarks>
/// Registration drives rule matching; matches are deferred to a <see cref="RuleQueue"/> and applied by
/// a <see cref="IRuleDriver"/>, and each subset remembers its cheapest member. Two drivers are provided:
/// the exhaustive bottom-up <see cref="IterativeRuleDriver"/> (the default) and the top-down
/// <see cref="TopDownRuleDriver"/> (Cascades), selected with <see cref="SetTopDownOpt"/>.
/// </remarks>
[Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner")]
public sealed class VolcanoPlanner : AbstractPlanner
{

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "allSets")]
    readonly List<NodeSet> _allSets = new List<NodeSet>();

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "mapDigestToRel")]
    readonly Dictionary<INodeDigest, INode> _digestToNode = new Dictionary<INodeDigest, INode>();

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "mapRel2Subset")]
    readonly Dictionary<INode, NodeSubset> _nodeToSubset = new Dictionary<INode, NodeSubset>(ReferenceEqualityComparer.Instance);

    // The rule-dispatch table: each node class maps to the operands that could bind a node of that class
    // (across every registered rule). Built incrementally as rules are added and as new node classes are
    // first seen, so that registering a node fires only the operands that can possibly match it.
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "classOperands")]
    readonly Dictionary<Type, List<RuleOperand>> _classOperands = new Dictionary<Type, List<RuleOperand>>();
    readonly HashSet<Type> _classes = new HashSet<Type>();

    // Nodes that have been pruned (importance zero): they are not added to a set on registration, and a
    // rule match touching one is skipped. Identity-based, like the nodes themselves.
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "prunedNodes")]
    readonly HashSet<INode> _prunedNodes = new HashSet<INode>(ReferenceEqualityComparer.Instance);

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "ruleDriver")]
    IRuleDriver _ruleDriver;

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "root")]
    NodeSubset? _root;

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "requestedRootTraits")]
    TraitSet? _requestedRootTraits;

    Cluster? _cluster;

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "topDownOpt")]
    bool _topDownOpt;

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "locked")]
    bool _locked;

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "nextSetId")]
    int _nextSetId;

    /// <summary>
    /// Creates a planner with an optional cost factory (defaulting to <see cref="VolcanoCost"/>).
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "VolcanoPlanner(RelOptCostFactory, Context)")]
    public VolcanoPlanner(ICostFactory? costFactory = null)
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
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "getRoot()")]
    public override INode? Root => _root;

    /// <summary>
    /// Whether the planner uses the top-down (Cascades) search rather than the default bottom-up search.
    /// </summary>
    public bool TopDownOpt => _topDownOpt;

    /// <summary>
    /// Chooses between the iterative (bottom-up) and top-down (Cascades) search strategies.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "setTopDownOpt(boolean)")]
    public void SetTopDownOpt(bool value)
    {
        _topDownOpt = value;
        _ruleDriver = value ? new TopDownRuleDriver(this) : new IterativeRuleDriver(this);
    }

    /// <summary>
    /// Locks or unlocks the planner. A locked planner accepts no new rules: <see cref="AddRule"/> does
    /// nothing and returns <c>false</c>.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "setLocked(boolean)")]
    public void SetLocked(bool locked) => _locked = locked;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "addRule(RelOptRule)")]
    public override bool AddRule(Rule rule)
    {
        if (_locked)
            return false;

        if (!base.AddRule(rule))
            return false;

        // Each of the rule's operands is an entry point for a match. Index every operand against the
        // concrete node classes already seen that could bind it — but a transformation rule is never
        // indexed against physical node classes, since it rewrites within a convention rather than
        // implementing one.
        bool isTransformationRule = rule is ITransformationRule;
        foreach (var operand in rule.Operands)
            foreach (var subClass in SubClasses(operand.MatchedClass))
            {
                if (isTransformationRule && typeof(IPhysicalNode).IsAssignableFrom(subClass))
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
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "removeRule(RelOptRule)")]
    public override bool RemoveRule(Rule rule)
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
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "clear()")]
    public override void Clear()
    {
        base.Clear();

        foreach (var rule in new List<Rule>(Rules))
            RemoveRule(rule);

        _classOperands.Clear();
        _classes.Clear();
        _allSets.Clear();
        _digestToNode.Clear();
        _nodeToSubset.Clear();
        _prunedNodes.Clear();
        _ruleDriver.Clear();
        _root = null;
        _requestedRootTraits = null;
        _cluster = null;
        _nextSetId = 0;
    }

    List<RuleOperand> OperandsFor(Type clazz)
    {
        if (!_classOperands.TryGetValue(clazz, out var operands))
        {
            operands = new List<RuleOperand>();
            _classOperands[clazz] = operands;
        }

        return operands;
    }

    /// <summary>
    /// The concrete node classes seen so far that are assignable to <paramref name="matchedClass"/>.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "subClasses(Class)")]
    IEnumerable<Type> SubClasses(Type matchedClass)
    {
        foreach (var clazz in _classes)
            if (matchedClass.IsAssignableFrom(clazz))
                yield return clazz;
    }

    /// <summary>
    /// Records a node's concrete class the first time it is seen, so that instances of it match the
    /// operands of every rule registered so far (and any registered later, via <see cref="AddRule"/>).
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "onNewClass(RelNode)")]
    void OnNewClass(INode node)
    {
        var clazz = node.GetType();
        if (!_classes.Add(clazz))
            return;

        bool isPhysical = typeof(IPhysicalNode).IsAssignableFrom(clazz);
        foreach (var rule in Rules)
        {
            // A transformation rule never matches a physical node.
            if (isPhysical && rule is ITransformationRule)
                continue;

            foreach (var operand in rule.Operands)
                if (operand.MatchedClass.IsAssignableFrom(clazz))
                    OperandsFor(clazz).Add(operand);
        }
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "setRoot(RelNode)")]
    public override void SetRoot(INode node)
    {
        _cluster ??= node.Cluster;
        _root = EnsureRegistered(node, null);
    }

    /// <inheritdoc />
    public override INode ChangeTraits(INode node, TraitSet toTraits)
    {
        _requestedRootTraits = toTraits;
        _root = (NodeSubset)Convert(node, toTraits);
        return _root;
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "changeTraits(RelNode, RelTraitSet)")]
    public override INode Convert(INode node, TraitSet toTraits)
    {
        var subset = EnsureRegistered(node, null);
        return EquivRoot(subset.Set).GetOrCreateSubset(toTraits, required: true);
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "findBestExp()")]
    public override INode FindBestPlan()
    {
        if (_root is null)
            throw new InvalidOperationException("No root has been set.");

        EnsureRootConverters();
        _ruleDriver.Drive();

        return _root.BuildCheapestPlan(this);
    }

    /// <summary>
    /// Registers a node as a member of the same set as <paramref name="equivalent"/> (or a fresh set
    /// when that is <c>null</c>), returning the subset it lands in.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "register(RelNode, RelNode)")]
    internal NodeSubset Register(INode node, INode? equivalent)
    {
        NodeSet? set = null;
        if (equivalent is not null)
        {
            var equivSubset = EnsureRegistered(equivalent, null);
            set = EquivRoot(equivSubset.Set);
        }

        return RegisterImpl(node, set);
    }

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "ensureRegistered(RelNode, RelNode)")]
    internal NodeSubset EnsureRegistered(INode node, INode? equivalent)
    {
        if (node is NodeSubset subset)
            return subset;

        if (_nodeToSubset.TryGetValue(node, out var existing))
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

        return Register(node, equivalent);
    }

    /// <summary>
    /// Resolves a subset to its live set: returns it unchanged if its set is still alive, else the
    /// equivalent subset on the set it was merged into.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "canonize(RelSubset)")]
    NodeSubset Canonize(NodeSubset subset)
    {
        if (subset.Set.EquivalentSet is null)
            return subset;

        return EquivRoot(subset.Set).GetOrCreateSubset(subset.Traits, subset.IsRequired);
    }

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "registerImpl(RelNode, RelSet)")]
    NodeSubset RegisterImpl(INode node, NodeSet? set)
    {
        if (node is NodeSubset subset)
            return RegisterSubset(set, subset);

        // A node must implement the interface its convention requires; a converter is exempt, since it
        // exists to bridge conventions.
        if (node is not IConverter && !node.Convention.Interface.IsInstanceOfType(node))
            throw new InvalidOperationException($"Node '{node.GetType().Name}' is not an instance of the '{node.Convention.Interface.Name}' interface required by convention '{node.Convention}'.");

        // Make sure the children are registered first, replacing each with its subset.
        node = OnRegister(node);

        // If an equivalent expression already exists, join its set (and carry over its pruned state).
        if (_digestToNode.TryGetValue(node.GetDigest(), out var equiv))
        {
            CheckPruned(equiv, node);
            var equivSubset = _nodeToSubset[equiv];
            return RegisterSubset(set, equivSubset);
        }

        // A converter lives in the same set as the input it converts.
        if (node is IConverter converter)
        {
            var childSet = EquivRoot(((NodeSubset)converter.Input).Set);
            if (set is not null && set != childSet && set.EquivalentSet is null)
                Merge(set, childSet);
            else
                set = childSet;
        }

        if (set is null)
        {
            set = new NodeSet(_nextSetId++, CostFactory, _cluster ??= node.Cluster);
            _allSets.Add(set);
        }

        set = EquivRoot(set);

        var added = AddNodeToSet(node, set);
        _digestToNode[node.GetDigest()] = node;

        foreach (var child in node.Children)
            ((NodeSubset)child).Set.Parents.Add(node);

        OnNewClass(node);
        FireRules(node);

        _ruleDriver.OnProduce(node, added);

        return added;
    }

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "registerSubset(RelSet, RelSubset)")]
    NodeSubset RegisterSubset(NodeSet? set, NodeSubset subset)
    {
        var live = EquivRoot(subset.Set);
        if (set is not null && EquivRoot(set) != live)
            Merge(set, live);

        return subset;
    }

    [Provenance("org.apache.calcite.rel.AbstractRelNode", "onRegister(RelOptPlanner)")]
    INode OnRegister(INode node)
    {
        if (node.Children.IsEmpty)
            return node;

        // A node requires its inputs in its own convention — a consumer asks for its inputs in a
        // particular convention — except a converter, which exists precisely to bridge conventions.
        // This propagates a lowering down the tree; the root, having no consumer, is handled separately
        // by the abstract converters added in EnsureRootConverters.
        var bridge = node is IConverter;
        var convention = node.Traits.Convention;

        var children = ImmutableArray.CreateBuilder<INode>(node.Children.Length);
        foreach (var child in node.Children)
        {
            var childSubset = EnsureRegistered(child, null);
            if (!bridge && !childSubset.Traits.Convention.Equals(convention))
                childSubset = childSubset.Set.GetOrCreateSubset(childSubset.Traits.Replace(ConventionTraitDef.Instance, convention));

            children.Add(childSubset);
        }

        return node.Copy(node.Traits, children.MoveToImmutable());
    }

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "addRelToSet(RelNode, RelSet)")]
    NodeSubset AddNodeToSet(INode node, NodeSet set)
    {
        var subset = set.Add(node);
        _nodeToSubset[node] = subset;
        PropagateCostImprovements(node);
        return subset;
    }

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "propagateCostImprovements(RelNode)")]
    internal void PropagateCostImprovements(INode node)
    {
        // A best-first worklist: nodes whose cost may have improved, processed cheapest-first. The cost
        // for each node lives in the map (read from there, not the heap); the heap only orders the work.
        var propagateRels = new Dictionary<INode, ICost>(ReferenceEqualityComparer.Instance);
        var propagateHeap = new PriorityQueue<INode, ICost>(Comparer<ICost>.Create(
            (a, b) => a.IsLessThan(b) ? -1 : b.IsLessThan(a) ? 1 : 0));

        propagateRels[node] = GetCost(node);
        propagateHeap.Enqueue(node, propagateRels[node]);

        while (propagateHeap.TryDequeue(out var relNode, out _))
        {
            var cost = propagateRels[relNode];

            foreach (var subset in GetSubsetNonNull(relNode).Set.Subsets)
            {
                if (!relNode.Traits.Satisfies(subset.Traits))
                    continue;

                // Not the current best and not cheaper than it → no change.
                if (!ReferenceEquals(relNode, subset.Best) && !cost.IsLessThan(subset.BestCost))
                    continue;

                // Already the best at the same cost → nothing to do.
                if (ReferenceEquals(relNode, subset.Best) && CostEquals(cost, subset.BestCost))
                    continue;

                subset.BestCost = cost;
                subset.Best = relNode;

                foreach (var parent in subset.GetParents())
                {
                    var newCost = GetCost(parent);
                    if (!propagateRels.TryGetValue(parent, out var existingCost) || newCost.IsLessThan(existingCost))
                    {
                        propagateRels[parent] = newCost;

                        // The reference removes the stale heap entry before re-offering; .NET's PriorityQueue
                        // has no remove, so the stale (higher-cost) entry stays and is harmlessly skipped when
                        // polled — the cost is read from the map, so re-polling a node is a no-op.
                        propagateHeap.Enqueue(parent, newCost);
                    }
                }
            }
        }
    }

    static bool CostEquals(ICost a, ICost b) => !a.IsLessThan(b) && !b.IsLessThan(a);

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "getCost(RelNode, RelMetadataQuery)")]
    ICost GetCost(INode node)
    {
        if (node is NodeSubset subset)
            return subset.BestCost;

        var cost = node.ComputeSelfCost(this);
        foreach (var child in node.Children)
            cost = cost.Plus(GetCost(child));

        return cost;
    }

    /// <summary>
    /// Fires every rule matched by a just-registered node. The dispatch table yields only the operands
    /// that could bind a node of this class; each such operand seeds a match that solves outward.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "fireRules(RelNode)")]
    internal void FireRules(INode node)
    {
        if (!_classOperands.TryGetValue(node.GetType(), out var operands))
            return;

        foreach (var operand in operands.ToArray())
            if (operand.Matches(node))
                new DeferringRuleCall(this, operand).Match(node);
    }

    /// <summary>
    /// The subset a registered node belongs to. (The traversal helpers <c>GetParentRels</c> / <c>GetRels</c>
    /// / <c>Contains</c> live on <see cref="NodeSubset"/>.)
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "getSubsetNonNull(RelNode)")]
    internal NodeSubset GetSubsetNonNull(INode node) => _nodeToSubset[node];

    /// <summary>
    /// The subset a node belongs to, or <c>null</c> if it is not registered (or has been removed).
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "getSubset(RelNode)")]
    internal NodeSubset? GetSubset(INode node) => _nodeToSubset.GetValueOrDefault(node);

    /// <summary>
    /// Prunes a node: marks it as having zero importance, so it is not added to a set on registration and
    /// no rule fires on a match that touches it.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "prune(RelNode)")]
    public override void Prune(INode node) => _prunedNodes.Add(node);

    /// <summary>
    /// Whether <paramref name="node"/> has been pruned.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "prunedNodes")]
    internal bool IsPruned(INode node) => _prunedNodes.Contains(node);

    /// <summary>
    /// Propagates pruning across a newly discovered equivalence: if <paramref name="duplicate"/> is
    /// pruned, then <paramref name="node"/> (equivalent to it) is pruned too.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "checkPruned(RelNode, RelNode)")]
    void CheckPruned(INode node, INode duplicate)
    {
        if (_prunedNodes.Contains(duplicate))
            _prunedNodes.Add(node);
    }

    // ~ Top-down (Cascades) search support -------------------------------------

    /// <summary>
    /// The root subset (typed), or <c>null</c> if no root has been set.
    /// </summary>
    internal NodeSubset? RootSubset => _root;

    /// <summary>
    /// The convention the root is requested in. A node in any other (non-physical) convention is logical.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "rootConvention")]
    internal IConvention? RootConvention => _root?.Traits.Convention;

    /// <summary>
    /// A zero cost from the active factory.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "zeroCost")]
    internal ICost ZeroCost => CostFactory.MakeZeroCost();

    /// <summary>
    /// An infinite cost from the active factory.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "infCost")]
    internal ICost InfiniteCost => CostFactory.MakeInfiniteCost();

    /// <summary>
    /// Whether a node is logical: not physical, and not already in the requested root convention.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "isLogical(RelNode)")]
    internal bool IsLogical(INode node)
    {
        return node is not IPhysicalNode && !node.Convention.Equals(RootConvention);
    }

    /// <summary>
    /// Whether a match is for a transformation rule.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "isTransformationRule(VolcanoRuleCall)")]
    internal bool IsTransformationRule(VolcanoRuleMatch match) => match.Rule is ITransformationRule;

    /// <summary>
    /// Whether a match is for a substitution rule. Alembic has no substitution rules.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "isSubstituteRule(VolcanoRuleCall)")]
    internal bool IsSubstituteRule(VolcanoRuleMatch match) => false;

    /// <summary>
    /// Whether a node has been registered.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "isRegistered(RelNode)")]
    internal bool IsRegistered(INode node) => _nodeToSubset.ContainsKey(node);

    /// <summary>
    /// The live set a registered node belongs to.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "getSet(RelNode)")]
    internal NodeSet GetSet(INode node) => EquivRoot(_nodeToSubset[node].Set);

    /// <summary>
    /// The lower bound of a node's cost. Pending the metadata subsystem this is the trivial zero bound,
    /// so the top-down search keeps its branch-and-bound structure but performs no lower-bound pruning.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "getLowerBound(RelNode)")]
    internal ICost GetLowerBound(INode node) => ZeroCost;

    /// <summary>
    /// The upper bound to allow a node's inputs, given the node's own upper bound: the bound minus the
    /// node's self cost.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "upperBoundForInputs(RelNode, RelOptCost)")]
    internal ICost UpperBoundForInputs(INode node, ICost upperBound)
    {
        if (!upperBound.IsInfinite)
        {
            var selfCost = node.ComputeSelfCost(this);
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
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "ensureRootConverters()")]
    void EnsureRootConverters()
    {
        if (_root is null || _requestedRootTraits is null)
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
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "changeTraitsUsingConverters(RelNode, RelTraitSet)")]
    internal INode? ChangeTraitsUsingConverters(INode node, TraitSet toTraits)
    {
        var subset = (NodeSubset)node;
        if (subset.Traits.Equals(toTraits))
            return subset;

        var path = FindConversionPath(subset.Traits, toTraits);
        if (path is null)
            return null;

        var current = subset;
        foreach (var step in path)
        {
            foreach (var member in EquivRoot(current.Set).Nodes.ToArray())
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

    List<ConversionStep>? FindConversionPath(TraitSet from, TraitSet to)
    {
        var visited = new HashSet<TraitSet> { from };
        var cameFrom = new Dictionary<TraitSet, (TraitSet Previous, ConversionStep Step)>();
        var queue = new Queue<TraitSet>();
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

    IEnumerable<(TraitSet Next, ConversionStep Step)> ConversionEdges(TraitSet current, TraitSet to)
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

    static List<ConversionStep> Reconstruct(Dictionary<TraitSet, (TraitSet Previous, ConversionStep Step)> cameFrom, TraitSet from, TraitSet to)
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

        public ConversionStep(TraitSet result, Func<INode, INode?> convert)
        {
            Result = result;
            Convert = convert;
        }

        public TraitSet Result { get; }

        public Func<INode, INode?> Convert { get; }

    }

    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "merge(RelSet, RelSet)")]
    void Merge(NodeSet set1, NodeSet set2)
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
    /// Whether <paramref name="set1"/> is less popular (fewer parents), smaller (fewer nodes), or younger
    /// (higher id) than <paramref name="set2"/> — in which case it is cheaper to merge it into the other.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "isSmaller(RelSet, RelSet)")]
    static bool IsSmaller(NodeSet set1, NodeSet set2)
    {
        if (set1.Parents.Count != set2.Parents.Count)
            return set1.Parents.Count < set2.Parents.Count;
        if (set1.Nodes.Count != set2.Nodes.Count)
            return set1.Nodes.Count < set2.Nodes.Count;

        return set1.Id > set2.Id;
    }

    /// <summary>
    /// Removes a set from the live registry (called by <see cref="NodeSet.MergeWith"/> when a set is
    /// absorbed). The C# analog of the upstream package-private <c>allSets.remove</c>.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "allSets")]
    internal void RemoveSet(NodeSet set) => _allSets.Remove(set);

    /// <summary>
    /// Records the subset a node now belongs to (called by <see cref="NodeSet.MergeWith"/> as it moves
    /// nodes into the surviving set).
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "mapRel2Subset")]
    internal void MapNodeToSubset(INode node, NodeSubset subset) => _nodeToSubset[node] = subset;

    /// <summary>
    /// Re-points a node's child subsets at their live (merged) sets, rebuilding the node if any child
    /// moved. Returns the rebuilt node, or <c>null</c> if nothing changed. The node is immutable, so the
    /// re-pointing produces a fresh copy rather than mutating in place.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "fixUpInputs(RelNode)")]
    INode? FixUpInputs(INode node)
    {
        var changed = false;
        var children = ImmutableArray.CreateBuilder<INode>(node.Children.Length);
        foreach (var child in node.Children)
        {
            var childSubset = (NodeSubset)child;
            var live = EquivRoot(childSubset.Set);
            var resolved = live == childSubset.Set ? childSubset : live.GetOrCreateSubset(childSubset.Traits);
            if (!ReferenceEquals(resolved, child))
                changed = true;

            children.Add(resolved);
        }

        if (!changed)
            return null;

        return node.Copy(node.Traits, children.MoveToImmutable());
    }

    /// <summary>
    /// Recomputes a node's digest after its children have been renamed (their sets merged), replacing it
    /// in place. If the recomputed node coincides with an existing one, their sets are merged instead.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "rename(RelNode)")]
    internal void Rename(INode node)
    {
        if (!_nodeToSubset.TryGetValue(node, out var subset))
            return;

        var rebuilt = FixUpInputs(node);
        if (rebuilt is null)
            return;

        var set = EquivRoot(subset.Set);

        // The old node is being replaced; drop its back-links from its (live) child sets so a stale node
        // never lingers in a set's parent list (one removal per input, matching how registration added them).
        foreach (var child in node.Children)
            EquivRoot(((NodeSubset)child).Set).ObliterateNode(node);

        _digestToNode.Remove(node.GetDigest());
        set.Nodes.Remove(node);
        _nodeToSubset.Remove(node);

        // The re-pointed node may now be a duplicate of one already registered; if so, fold the sets
        // together rather than keeping two copies (carrying over the pruned state).
        if (_digestToNode.TryGetValue(rebuilt.GetDigest(), out var equiv))
        {
            CheckPruned(equiv, node);

            // Any subset whose best was the removed node now uses its equivalent.
            foreach (var s in set.Subsets)
            {
                if (ReferenceEquals(s.Best, node))
                {
                    s.Best = equiv;
                    PropagateCostImprovements(equiv);
                }
            }

            var equivSet = EquivRoot(_nodeToSubset[equiv].Set);
            if (equivSet != set)
                Merge(set, equivSet);

            return;
        }

        var added = set.Add(rebuilt);
        _nodeToSubset[rebuilt] = added;
        _digestToNode[rebuilt.GetDigest()] = rebuilt;

        // Link the rebuilt node into its (live) child sets' parent lists.
        foreach (var child in rebuilt.Children)
            EquivRoot(((NodeSubset)child).Set).Parents.Add(rebuilt);

        PropagateCostImprovements(rebuilt);
    }

    /// <summary>
    /// The live representative of a set: the set itself, or the set it was merged into (following the
    /// chain).
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoPlanner", "equivRoot(RelSet)")]
    internal static NodeSet EquivRoot(NodeSet set)
    {
        while (set.EquivalentSet is not null)
            set = set.EquivalentSet;

        return set;
    }

}
