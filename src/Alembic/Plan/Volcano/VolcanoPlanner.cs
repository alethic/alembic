using System;
using System.Collections.Generic;
using System.Collections.Immutable;

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
/// a <see cref="IRuleDriver"/> until the queue is empty, and each subset remembers its cheapest member.
/// Only the exhaustive iterative driver is provided; an importance-guided or top-down driver is future
/// work.
/// </remarks>
public sealed class VolcanoPlanner : AbstractPlanner
{

    readonly List<NodeSet> _allSets = new List<NodeSet>();
    readonly Dictionary<INodeDigest, INode> _digestToNode = new Dictionary<INodeDigest, INode>();
    readonly Dictionary<INode, NodeSubset> _nodeToSubset = new Dictionary<INode, NodeSubset>(ReferenceEqualityComparer.Instance);

    IRuleDriver _ruleDriver;
    NodeSubset? _root;
    TraitSet? _requestedRootTraits;
    bool _topDownOpt;
    int _nextSetId;

    /// <summary>
    /// Creates a planner with an optional cost factory (defaulting to <see cref="VolcanoCost"/>).
    /// </summary>
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

    /// <summary>
    /// The root subset being optimized, once a root has been set.
    /// </summary>
    internal NodeSubset? Root => _root;

    /// <summary>
    /// Whether the planner uses a top-down (Cascades) search. Only the iterative search is
    /// implemented, so this can only be left at its default of <c>false</c>.
    /// </summary>
    public bool TopDownOpt => _topDownOpt;

    /// <summary>
    /// Chooses between the iterative and top-down search strategies. Only the iterative strategy is
    /// available; requesting top-down throws.
    /// </summary>
    public void SetTopDownOpt(bool value)
    {
        if (value)
            throw new NotSupportedException("The top-down (Cascades) rule driver is not implemented; only the iterative driver is available.");

        _topDownOpt = value;
        _ruleDriver = new IterativeRuleDriver(this);
    }

    /// <inheritdoc />
    public override void SetRoot(INode node)
    {
        _root = EnsureRegistered(node, null);
    }

    /// <inheritdoc />
    public override INode ChangeTraits(INode node, TraitSet toTraits)
    {
        var subset = EnsureRegistered(node, null);
        _requestedRootTraits = toTraits;
        _root = subset.Traits.Equals(toTraits) ? subset : subset.Set.GetOrCreateSubset(toTraits);
        return _root;
    }

    /// <inheritdoc />
    public override INode FindBestPlan()
    {
        if (_root is null)
            throw new InvalidOperationException("No root has been set.");

        EnsureRootConverters();
        _ruleDriver.Drive();

        var plan = BuildCheapestPlan(_root);
        FireNodeChosen(null);
        return plan;
    }

    /// <summary>
    /// Registers a node as a member of the same set as <paramref name="equivalent"/> (or a fresh set
    /// when that is <c>null</c>), returning the subset it lands in.
    /// </summary>
    internal NodeSubset Register(INode node, INode? equivalent)
    {
        NodeSet? set = null;
        if (equivalent is not null)
        {
            var equivSubset = EnsureRegistered(equivalent, null);
            set = Live(equivSubset.Set);
        }

        return RegisterImpl(node, set);
    }

    NodeSubset EnsureRegistered(INode node, INode? equivalent)
    {
        if (node is NodeSubset subset)
            return subset;

        if (_nodeToSubset.TryGetValue(node, out var existing))
            return existing;

        return Register(node, equivalent);
    }

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

        // If an equivalent expression already exists, join its set.
        if (_digestToNode.TryGetValue(node.GetDigest(), out var equiv))
        {
            var equivSubset = _nodeToSubset[equiv];
            return RegisterSubset(set, equivSubset);
        }

        // A converter lives in the same set as the input it converts.
        if (node is IConverter converter)
        {
            var childSet = Live(((NodeSubset)converter.Input).Set);
            if (set is not null && set != childSet && set.EquivalentSet is null)
                Merge(set, childSet);
            else
                set = childSet;
        }

        if (set is null)
        {
            set = new NodeSet(_nextSetId++, CostFactory);
            _allSets.Add(set);
        }

        set = Live(set);

        var added = AddNodeToSet(node, set);
        _digestToNode[node.GetDigest()] = node;

        foreach (var child in node.Children)
            ((NodeSubset)child).Set.Parents.Add(node);

        FireRules(node);

        return added;
    }

    NodeSubset RegisterSubset(NodeSet? set, NodeSubset subset)
    {
        var live = Live(subset.Set);
        if (set is not null && Live(set) != live)
            Merge(set, live);

        return subset;
    }

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

    NodeSubset AddNodeToSet(INode node, NodeSet set)
    {
        var subset = set.Add(node);
        _nodeToSubset[node] = subset;
        PropagateCostImprovements(node);
        FireNodeEquivalenceFound(node);
        return subset;
    }

    void PropagateCostImprovements(INode node)
    {
        var cost = GetCost(node);
        var set = Live(_nodeToSubset[node].Set);

        foreach (var subset in set.Subsets)
        {
            if (!node.Traits.Satisfies(subset.Traits))
                continue;

            if (!cost.IsLessThan(subset.BestCost))
                continue;

            subset.BestCost = cost;
            subset.Best = node;

            foreach (var parent in set.Parents.ToArray())
                PropagateCostImprovements(parent);
        }
    }

    ICost GetCost(INode node)
    {
        if (node is NodeSubset subset)
            return subset.BestCost;

        var cost = node.ComputeSelfCost(this);
        foreach (var child in node.Children)
            cost = cost.Plus(GetCost(child));

        return cost;
    }

    void FireRules(INode node)
    {
        foreach (var rule in Rules)
            new DeferringRuleCall(this, rule).Match(node);
    }

    /// <summary>
    /// Enumerates every way the operand pattern matches the node, returning the operand-bound nodes for
    /// each match (a node's children are subsets, so the matcher descends into a subset's members).
    /// </summary>
    internal IEnumerable<ImmutableArray<INode>> MatchBindings(Operand operand, INode node)
    {
        if (!operand.Predicate(node))
            yield break;

        if (operand.Children.IsEmpty)
        {
            yield return ImmutableArray.Create(node);
            yield break;
        }

        if (node.Children.Length != operand.Children.Length)
            yield break;

        var perChild = new List<List<ImmutableArray<INode>>>(operand.Children.Length);
        for (int i = 0; i < operand.Children.Length; i++)
        {
            var bindings = new List<ImmutableArray<INode>>(MatchChild(operand.Children[i], node.Children[i]));
            if (bindings.Count == 0)
                yield break;

            perChild.Add(bindings);
        }

        foreach (var combo in CartesianProduct(perChild))
        {
            var builder = ImmutableArray.CreateBuilder<INode>();
            builder.Add(node);
            foreach (var part in combo)
                builder.AddRange(part);

            yield return builder.ToImmutable();
        }
    }

    IEnumerable<ImmutableArray<INode>> MatchChild(Operand operand, INode child)
    {
        // An operand with no further structure that accepts the subset binds the subset itself
        // (the "matches any child" case).
        if (operand.Children.IsEmpty && operand.Predicate(child))
        {
            yield return ImmutableArray.Create(child);
            yield break;
        }

        if (child is not NodeSubset subset)
            yield break;

        foreach (var member in Live(subset.Set).Nodes)
        {
            if (!member.Traits.Satisfies(subset.Traits))
                continue;

            foreach (var binding in MatchBindings(operand, member))
                yield return binding;
        }
    }

    static IEnumerable<List<ImmutableArray<INode>>> CartesianProduct(List<List<ImmutableArray<INode>>> lists)
    {
        var result = new List<List<ImmutableArray<INode>>> { new List<ImmutableArray<INode>>() };
        foreach (var list in lists)
        {
            var next = new List<List<ImmutableArray<INode>>>();
            foreach (var partial in result)
            {
                foreach (var item in list)
                {
                    var extended = new List<ImmutableArray<INode>>(partial) { item };
                    next.Add(extended);
                }
            }

            result = next;
        }

        return result;
    }

    INode BuildCheapestPlan(NodeSubset root)
    {
        return BuildCheapest(root, new Dictionary<NodeSubset, INode>());
    }

    INode BuildCheapest(NodeSubset subset, Dictionary<NodeSubset, INode> memo)
    {
        if (memo.TryGetValue(subset, out var done))
            return done;

        var best = subset.Best;
        if (best is null)
            throw new CannotPlanException($"There are not enough rules to produce a node with the requested traits ({subset.Traits.Convention}).");

        FireNodeChosen(best);

        var children = best.Children;
        ImmutableArray<INode>.Builder? builder = null;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] is NodeSubset childSubset)
            {
                builder ??= children.ToBuilder();
                builder[i] = BuildCheapest(childSubset, memo);
            }
        }

        var result = builder is null ? best : best.Copy(best.Traits, builder.ToImmutable());
        memo[subset] = result;
        return result;
    }

    /// <summary>
    /// Puts an <see cref="AbstractConverter"/> into the root's set for each delivered subset whose
    /// traits differ from the requested root traits. The root is the only place explicit converters are
    /// needed — everywhere else a parent has already asked for its inputs' convention (see
    /// <see cref="OnRegister"/>). <see cref="ExpandConversionRule"/> then turns each one into a real
    /// conversion.
    /// </summary>
    void EnsureRootConverters()
    {
        if (_root is null || _requestedRootTraits is null)
            return;

        var set = Live(_root.Set);
        foreach (var subset in set.Subsets.ToArray())
        {
            if (subset.Traits.Equals(_root.Traits))
                continue;

            Register(new AbstractConverter(_root.Traits, subset), _root);
        }
    }

    /// <summary>
    /// Converts a subset to the given traits by applying the registered converter rules to its members,
    /// returning the target subset once it has a member (or <c>null</c> if none can be produced).
    /// </summary>
    internal INode? ChangeTraitsUsingConverters(INode node, TraitSet toTraits)
    {
        var subset = (NodeSubset)node;
        var set = Live(subset.Set);
        if (subset.Traits.Equals(toTraits))
            return subset;

        foreach (var rule in Rules)
        {
            if (rule is not IConverterRule converter)
                continue;

            if (!subset.Traits.Get(converter.Source.Def).Equals(converter.Source) || !toTraits.Get(converter.Target.Def).Equals(converter.Target))
                continue;

            foreach (var member in set.Nodes.ToArray())
            {
                if (!member.Traits.Satisfies(subset.Traits))
                    continue;

                var converted = converter.Convert(member);
                if (converted is not null)
                    Register(converted, member);
            }
        }

        var target = Live(subset.Set).GetSubset(toTraits);
        return target is not null && target.Best is not null ? target : null;
    }

    void Merge(NodeSet set1, NodeSet set2)
    {
        set1 = Live(set1);
        set2 = Live(set2);
        if (set1 == set2)
            return;

        set2.EquivalentSet = set1;
        _allSets.Remove(set2);

        foreach (var node in set2.Nodes)
        {
            var subset = set1.Add(node);
            _nodeToSubset[node] = subset;
            PropagateCostImprovements(node);
        }

        set1.Parents.AddRange(set2.Parents);

        foreach (var node in set2.Nodes)
            FireRules(node);
    }

    static NodeSet Live(NodeSet set)
    {
        while (set.EquivalentSet is not null)
            set = set.EquivalentSet;

        return set;
    }

}
