using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Algebra.Convert;
using Alembic.Plan.Rules;
using Alembic.Util.Graph;

namespace Alembic.Plan.Hep;

/// <summary>
/// A heuristic planner: it applies its program's rules to a shared graph of the plan, in the configured
/// order, until no rule changes anything. Deterministic and program-driven.
/// </summary>
/// <remarks>
/// The plan is a single-rooted DAG of <see cref="HepNodeVertex"/>: equal subexpressions are interned to
/// one vertex, so a rule fires once per distinct subexpression and a rewrite is shared by every parent.
/// A rewrite replaces a vertex's content and re-points its parents; discarded vertices are reclaimed by
/// mark-and-sweep garbage collection.
/// </remarks>
public sealed class HepPlanner : AbstractPlanner
{

    readonly HepProgram _mainProgram;
    readonly Dictionary<INodeDigest, HepNodeVertex> _mapDigestToVertex = new Dictionary<INodeDigest, HepNodeVertex>();
    readonly DirectedGraph<HepNodeVertex, DefaultEdge> _graph = DefaultDirectedGraph<HepNodeVertex, DefaultEdge>.Create();
    readonly Dictionary<FiredKey, HashSet<Rule>> _firedRulesCache = new Dictionary<FiredKey, HashSet<Rule>>();
    readonly Dictionary<INode, HashSet<FiredKey>> _firedRulesCacheIndex = new Dictionary<INode, HashSet<FiredKey>>();

    HepNodeVertex? _root;
    INode? _rootNode;
    TraitSet? _requestedRootTraits;
    int _nTransformations;
    int _graphSizeLastGC;
    int _nTransformationsLastGC;
    bool _noDag;
    bool _enableFiredRulesCache;
    bool _largePlanMode;

    /// <summary>
    /// Creates a planner driven by the given program. More rules can be added with
    /// <see cref="AbstractPlanner.AddRule"/> (e.g. by a convention's <see cref="ITrait.Register"/>).
    /// </summary>
    public HepPlanner(HepProgram program)
    {
        _mainProgram = program;
    }

    /// <summary>
    /// Creates a planner driven by the given program, costing nodes with <paramref name="costFactory"/>.
    /// </summary>
    public HepPlanner(HepProgram program, ICostFactory costFactory)
        : base(costFactory)
    {
        _mainProgram = program;
    }

    /// <summary>
    /// Creates a planner with an empty program, in large-plan mode with the fired-rules cache on, for
    /// running several programs in succession over a preserved graph (multi-phase optimization).
    /// </summary>
    public HepPlanner()
        : this(HepProgram.Builder().Build())
    {
        _largePlanMode = true;
        _enableFiredRulesCache = true;
    }

    /// <summary>
    /// Whether the planner skips common-subexpression sharing, keeping the graph a tree.
    /// </summary>
    public bool NoDag
    {
        get => _noDag;
        set => _noDag = value;
    }

    /// <summary>
    /// Whether large-plan mode is on (favours fine-grained garbage collection over periodic sweeps).
    /// </summary>
    public bool LargePlanMode
    {
        get => _largePlanMode;
        set => _largePlanMode = value;
    }

    /// <summary>
    /// Enables the fired-rules cache: a rule will not fire twice on the same match.
    /// </summary>
    public void SetEnableFiredRulesCache(bool enable)
    {
        _enableFiredRulesCache = enable;
    }

    /// <inheritdoc />
    public override void SetRoot(INode node)
    {
        _rootNode = node;
        _root = AddNodeToGraph(node);
    }

    /// <inheritdoc />
    public override INode? Root => _root;

    /// <inheritdoc />
    public override void Clear()
    {
        base.Clear();
        ClearRules();
    }

    /// <summary>
    /// Removes all rules and the fired-rules caches, keeping the graph for reuse across programs.
    /// </summary>
    public void ClearRules()
    {
        foreach (var rule in new List<Rule>(Rules))
            RemoveRule(rule);

        _firedRulesCache.Clear();
        _firedRulesCacheIndex.Clear();
    }

    /// <summary>
    /// Ignores traits except for the root, where it remembers what the final conversion should be; the
    /// request is enforced when <see cref="FindBestPlan"/> finishes.
    /// </summary>
    public override INode ChangeTraits(INode node, TraitSet toTraits)
    {
        if (ReferenceEquals(node, _rootNode) || (_root is not null && ReferenceEquals(node, _root.CurrentNode)))
            _requestedRootTraits = toTraits;

        return node;
    }

    /// <summary>
    /// The heuristic planner has no equivalence subsets and rewrites over concrete nodes, so a trait
    /// conversion is a no-op here. (The top-down trait derivation that uses this runs only under the
    /// cost-based planner.)
    /// </summary>
    public override INode Convert(INode node, TraitSet toTraits) => node;

    /// <inheritdoc />
    public override INode FindBestPlan()
    {
        if (_root is null)
            throw new InvalidOperationException("No root has been set.");

        ExecuteProgram(_mainProgram);

        // Get rid of everything except what's in the final plan.
        CollectGarbage();

        var plan = BuildFinalPlan(_root, new Dictionary<HepNodeVertex, INode>());

        if (_requestedRootTraits is not null)
            EnsureSatisfies(plan, _requestedRootTraits);

        return plan;
    }

    /// <summary>
    /// Verifies that every node in the plan carries the requested traits. Because conversions rewrite
    /// nodes in place rather than wrapping them, a complete plan is uniform throughout, so a single
    /// surviving node that falls short means no rule chain could finish the job.
    /// </summary>
    static void EnsureSatisfies(INode node, TraitSet required)
    {
        if (!node.Traits.Satisfies(required))
            throw new CannotPlanException($"No plan satisfies the requested traits; '{node.GetType().Name}' remained in convention '{node.Convention}'.");

        foreach (var child in node.Children)
            EnsureSatisfies(child, required);
    }

    // ~ Program execution ----------------------------------------------------

    /// <summary>
    /// Top-level entry point for a program: prepares its state and runs it.
    /// </summary>
    internal void ExecuteProgram(HepProgram program)
    {
        var px = HepInstruction.PrepareContext.Create(this);
        var state = program.Prepare(px);
        state.Execute();
    }

    internal void ExecuteProgram(HepProgram program, HepProgram.State state)
    {
        state.Init();
        foreach (var instructionState in state.InstructionStates)
        {
            instructionState.Execute();

            var delta = _nTransformations - _nTransformationsLastGC;
            if (!_largePlanMode && delta > _graphSizeLastGC)
            {
                // Enough has changed since the last collection that there should be garbage now. Doing it
                // here amortizes the cost across instructions while keeping memory proportional to size.
                CollectGarbage();
            }
        }
    }

    internal void ExecuteMatchLimit(HepInstruction.MatchLimit instruction, HepInstruction.MatchLimit.State state)
    {
        state.ProgramState!.MatchLimit = instruction.Limit;
    }

    internal void ExecuteMatchOrder(HepInstruction.MatchOrder instruction, HepInstruction.MatchOrder.State state)
    {
        state.ProgramState!.MatchOrder = instruction.Order;
    }

    internal void ExecuteRuleInstance(HepInstruction.RuleInstance instruction, HepInstruction.RuleInstance.State state)
    {
        if (state.ProgramState!.SkippingGroup())
            return;

        ApplyRules(state.ProgramState, new[] { instruction.Rule }, true);
    }

    internal void ExecuteRuleLookup(HepInstruction.RuleLookup instruction, HepInstruction.RuleLookup.State state)
    {
        if (state.ProgramState!.SkippingGroup())
            return;

        state.Rule ??= GetRuleByDescription(instruction.RuleDescription);
        if (state.Rule is not null)
            ApplyRules(state.ProgramState, new[] { state.Rule }, true);
    }

    internal void ExecuteRuleClass(HepInstruction.RuleClass instruction, HepInstruction.RuleClass.State state)
    {
        if (state.ProgramState!.SkippingGroup())
            return;

        if (state.RuleSet is null)
        {
            state.RuleSet = new HashSet<Rule>();
            foreach (var rule in Rules)
                if (instruction.RuleType.IsInstanceOfType(rule))
                    state.RuleSet.Add(rule);
        }

        ApplyRules(state.ProgramState, state.RuleSet, true);
    }

    internal void ExecuteRuleCollection(HepInstruction.RuleCollection instruction, HepInstruction.RuleCollection.State state)
    {
        if (state.ProgramState!.SkippingGroup())
            return;

        ApplyRules(state.ProgramState, instruction.Rules, true);
    }

    internal void ExecuteConverterRules(HepInstruction.ConverterRules instruction, HepInstruction.ConverterRules.State state)
    {
        if (state.RuleSet is null)
        {
            state.RuleSet = new HashSet<Rule>();
            foreach (var rule in Rules)
            {
                if (rule is not ConverterRule converter || converter.IsGuaranteed != instruction.Guaranteed)
                    continue;

                // Add the converter to work top-down.
                state.RuleSet.Add(converter);

                // A non-guaranteed converter also gets a trait-matching wrapper to work bottom-up.
                if (!instruction.Guaranteed)
                    state.RuleSet.Add(new TraitMatchingRule(converter));
            }
        }

        ApplyRules(state.ProgramState!, state.RuleSet, instruction.Guaranteed);
    }

    internal void ExecuteCommonRelSubExprRules(HepInstruction.CommonRelSubExprRules instruction, HepInstruction.CommonRelSubExprRules.State state)
    {
        if (state.RuleSet is null)
        {
            state.RuleSet = new HashSet<Rule>();
            foreach (var rule in Rules)
                if (rule is ICommonSubExprRule)
                    state.RuleSet.Add(rule);
        }

        ApplyRules(state.ProgramState!, state.RuleSet, true);
    }

    internal void ExecuteSubProgram(HepInstruction.SubProgram instruction, HepInstruction.SubProgram.State state)
    {
        for (; ; )
        {
            var before = _nTransformations;
            state.SubProgramState.Execute();
            if (_nTransformations == before)
                break;
        }
    }

    internal void ExecuteBeginGroup(HepInstruction.BeginGroup instruction, HepInstruction.BeginGroup.State state)
    {
        state.ProgramState!.Group = state.EndGroup;
    }

    internal void ExecuteEndGroup(HepInstruction.EndGroup instruction, HepInstruction.EndGroup.State state)
    {
        state.ProgramState!.Group = null;
        state.Collecting = false;
        ApplyRules(state.ProgramState, state.RuleSet, true);
    }

    // ~ Rule application -----------------------------------------------------

    int DepthFirstApply(HepProgram.State programState, IEnumerator<HepNodeVertex> iter, IReadOnlyList<Rule> rules, bool forceConversions, int nMatches)
    {
        while (iter.MoveNext())
        {
            var vertex = iter.Current;
            foreach (var rule in rules)
            {
                var newVertex = ApplyRule(rule, vertex, forceConversions);
                if (newVertex is null || ReferenceEquals(newVertex, vertex))
                    continue;

                ++nMatches;
                if (nMatches >= programState.MatchLimit)
                    return nMatches;

                // Pick up where we left off; the old iterator was invalidated by the transformation.
                var depthIter = GetGraphIterator(programState, newVertex);
                nMatches = DepthFirstApply(programState, depthIter, rules, forceConversions, nMatches);
                break;
            }
        }

        return nMatches;
    }

    void ApplyRules(HepProgram.State programState, IEnumerable<Rule> rules, bool forceConversions)
    {
        var group = programState.Group;
        if (group is not null)
        {
            foreach (var rule in rules)
                group.RuleSet.Add(rule);

            return;
        }

        var ruleList = rules as IReadOnlyList<Rule> ?? new List<Rule>(rules);
        var fullRestart = programState.MatchOrder != HepMatchOrder.Arbitrary && programState.MatchOrder != HepMatchOrder.DepthFirst;

        // In large-plan mode an unordered/depth-first pass uses a vertex iterator that can resume from a
        // new vertex rather than restarting from the root each time, avoiding revisiting stable subgraphs.
        var useHepVertexIterator = (programState.MatchOrder == HepMatchOrder.Arbitrary || programState.MatchOrder == HepMatchOrder.DepthFirst) && _largePlanMode;
        var nMatches = 0;

        bool fixedPoint;
        do
        {
            IEnumerator<HepNodeVertex> iter = useHepVertexIterator
                ? new HepVertexIterator(_root!, new HashSet<HepNodeVertex>())
                : GetGraphIterator(programState, _root!);
            fixedPoint = true;
            while (iter.MoveNext())
            {
                var vertex = iter.Current;
                foreach (var rule in ruleList)
                {
                    var newVertex = ApplyRule(rule, vertex, forceConversions);
                    if (newVertex is null || ReferenceEquals(newVertex, vertex))
                        continue;

                    ++nMatches;
                    if (nMatches >= programState.MatchLimit)
                        return;

                    if (fullRestart)
                    {
                        iter = GetGraphIterator(programState, _root!);
                    }
                    else
                    {
                        // Pick up where we left off; the old iterator was invalidated by the transformation.
                        iter = useHepVertexIterator
                            ? ((HepVertexIterator)iter).ContinueFrom(newVertex)
                            : GetGraphIterator(programState, newVertex);
                        if (programState.MatchOrder == HepMatchOrder.DepthFirst)
                        {
                            nMatches = DepthFirstApply(programState, iter, ruleList, forceConversions, nMatches);
                            if (nMatches >= programState.MatchLimit)
                                return;
                        }

                        // Remember to go around again since we skipped some vertices.
                        fixedPoint = false;
                    }

                    break;
                }
            }
        }
        while (!fixedPoint);
    }

    IEnumerator<HepNodeVertex> GetGraphIterator(HepProgram.State programState, HepNodeVertex start)
    {
        switch (programState.MatchOrder)
        {
            case HepMatchOrder.Arbitrary:
            case HepMatchOrder.DepthFirst:
                if (_largePlanMode)
                    return new HepVertexIterator(start, new HashSet<HepNodeVertex>());
                return new DepthFirstIterator<HepNodeVertex, DefaultEdge>(_graph, start);

            case HepMatchOrder.TopDown:
            case HepMatchOrder.BottomUp:
                // tryCleanVertices already removes the vertices a transformation orphaned, so the
                // topological order is over the live graph.
                if (!_largePlanMode)
                    CollectGarbage();
                return new TopologicalOrderIterator<HepNodeVertex, DefaultEdge>(_graph, programState.MatchOrder);

            default:
                throw new NotSupportedException("Unsupported match order: " + programState.MatchOrder);
        }
    }

    HepNodeVertex? ApplyRule(Rule rule, HepNodeVertex vertex, bool forceConversions)
    {
        if (IsRuleExcluded(rule))
            return null;

        if (!_graph.VertexSet.Contains(vertex))
            return null;

        ITrait? parentTrait = null;
        if (rule is ConverterRule converter)
        {
            // Converter rules fire only where the conversion is actually wanted, or they run away.
            if (!DoesConverterApply(converter, vertex))
                return null;

            parentTrait = converter.Target;
        }
        else if (rule is ICommonSubExprRule)
        {
            // Only fire on a genuine common subexpression — a vertex with more than one parent.
            if (GetVertexParents(vertex).Count < 2)
                return null;
        }

        var bindings = Match(rule.Operand, vertex.CurrentNode);
        if (bindings is null)
            return null;

        FiredKey? key = null;
        if (_enableFiredRulesCache)
        {
            key = new FiredKey(bindings.Value);
            if (_firedRulesCache.TryGetValue(key, out var fired) && fired.Contains(rule))
                return null;
        }

        var call = new HepRuleCall(this, rule.Operand, bindings.Value);

        // Let the rule apply its own side-condition.
        if (!rule.Matches(call))
            return null;

        FireRuleAttempted(call, true);
        rule.OnMatch(call);
        FireRuleAttempted(call, false);

        if (key is not null)
        {
            if (!_firedRulesCache.TryGetValue(key, out var fired))
                _firedRulesCache[key] = fired = new HashSet<Rule>();
            fired.Add(rule);

            foreach (var node in bindings.Value)
            {
                if (!_firedRulesCacheIndex.TryGetValue(node, out var keys))
                    _firedRulesCacheIndex[node] = keys = new HashSet<FiredKey>();
                keys.Add(key);
            }
        }

        if (call.Results.Count > 0)
            return ApplyTransformationResults(vertex, call, parentTrait);

        return null;
    }

    bool DoesConverterApply(ConverterRule converter, HepNodeVertex vertex)
    {
        var outTrait = converter.Target;
        foreach (var parent in Graphs.PredecessorListOf(_graph, vertex))
        {
            var parentRel = parent.CurrentNode;
            if (parentRel is IConverter)
                continue; // We don't support converter chains.

            if (parentRel.Traits.Contains(outTrait))
                return true; // This parent wants the traits the converter produces.
        }

        return ReferenceEquals(vertex, _root)
            && _requestedRootTraits is not null
            && _requestedRootTraits.Contains(outTrait);
    }

    /// <summary>
    /// The parent vertices of a vertex, counted once per input reference.
    /// </summary>
    List<HepNodeVertex> GetVertexParents(HepNodeVertex vertex)
    {
        var parents = new List<HepNodeVertex>();
        foreach (var parentVertex in Graphs.PredecessorListOf(_graph, vertex))
        {
            var parent = parentVertex.CurrentNode;
            foreach (var child in parent.Children)
                if (ReferenceEquals(child, vertex))
                    parents.Add(parentVertex);
        }

        return parents;
    }

    HepNodeVertex ApplyTransformationResults(HepNodeVertex vertex, HepRuleCall call, ITrait? parentTrait)
    {
        INode? bestRel;
        if (call.Results.Count == 1)
        {
            bestRel = call.Results[0];
        }
        else
        {
            bestRel = null;
            ICost? bestCost = null;
            foreach (var rel in call.Results)
            {
                var thisCost = GetCost(rel);
                if (bestRel is null || thisCost.IsLessThan(bestCost!))
                {
                    bestRel = rel;
                    bestCost = thisCost;
                }
            }
        }

        ++_nTransformations;

        // Snapshot the parents before adding the result, so contraction only re-points existing parents,
        // not new ones (which would create loops). Filter by trait when this is a converter rule.
        var parents = new List<HepNodeVertex>();
        foreach (var parent in Graphs.PredecessorListOf(_graph, vertex))
        {
            if (parentTrait is not null)
            {
                var parentRel = parent.CurrentNode;
                if (parentRel is IConverter)
                    continue;

                if (!parentRel.Traits.Contains(parentTrait))
                    continue; // This parent does not want the converted result.
            }

            parents.Add(parent);
        }

        var newVertex = AddNodeToGraph(bestRel!);
        var garbage = new HashSet<HepNodeVertex>();

        // The new vertex may already be one of the parents (common subexpression); treat that as a nop
        // to avoid creating a loop.
        var parentMatch = parents.IndexOf(newVertex);
        if (parentMatch != -1)
            newVertex = parents[parentMatch];
        else
            ContractVertices(newVertex, vertex, parents, garbage);

        if (_largePlanMode)
            CollectGarbage(garbage);
        else if (HasListeners)
            CollectGarbage();

        FireRuleProductionSucceeded(call, bestRel!);

        return newVertex;
    }

    static bool ShallowEqual(ImmutableArray<INode> a, IReadOnlyList<INode> b)
    {
        if (a.Length != b.Count)
            return false;

        for (int i = 0; i < a.Length; i++)
            if (!ReferenceEquals(a[i], b[i]))
                return false;

        return true;
    }

    HepNodeVertex AddNodeToGraph(INode rel)
    {
        if (rel is HepNodeVertex existing && _graph.VertexSet.Contains(existing))
            return existing;

        // Recursively add children, replacing this node's inputs with their vertices.
        var newInputs = new List<INode>(rel.Children.Length);
        foreach (var input in rel.Children)
            newInputs.Add(AddNodeToGraph(input));

        if (!ShallowEqual(rel.Children, newInputs))
            rel = rel.Copy(rel.Traits, newInputs.ToImmutableArray());

        if (!_noDag && _mapDigestToVertex.TryGetValue(rel.GetDigest(), out var equivVertex))
            return equivVertex; // Use the existing equivalent vertex.

        var newVertex = new HepNodeVertex(rel);
        _graph.AddVertex(newVertex);
        UpdateVertex(newVertex, rel);

        foreach (var input in rel.Children)
            _graph.AddEdge(newVertex, (HepNodeVertex)input);

        _nTransformations++;
        return newVertex;
    }

    void ContractVertices(HepNodeVertex preservedVertex, HepNodeVertex discardedVertex, List<HepNodeVertex> parents, HashSet<HepNodeVertex> garbage)
    {
        if (ReferenceEquals(preservedVertex, discardedVertex))
            return;

        UpdateVertex(preservedVertex, preservedVertex.CurrentNode);

        foreach (var parent in parents)
        {
            var parentRel = parent.CurrentNode;
            var inputs = parentRel.Children;
            ImmutableArray<INode>.Builder? builder = null;
            for (int i = 0; i < inputs.Length; i++)
            {
                if (ReferenceEquals(inputs[i], discardedVertex))
                {
                    builder ??= inputs.ToBuilder();
                    builder[i] = preservedVertex;
                }
            }

            // Nodes are immutable, so re-point a parent by rebuilding it rather than mutating in place.
            if (builder is not null)
                parentRel = parentRel.Copy(parentRel.Traits, builder.ToImmutable());

            ClearCache(parent);
            _graph.RemoveEdge(parent, discardedVertex);
            _graph.AddEdge(parent, preservedVertex);
            UpdateVertex(parent, parentRel);
        }

        // We don't remove discardedVertex now (it may still be reachable from preservedVertex); garbage
        // collection takes care of it.
        if (ReferenceEquals(discardedVertex, _root))
            _root = preservedVertex;

        garbage.Add(discardedVertex);
    }

    /// <summary>
    /// Clears the cached digest of a vertex and its ancestors, so they are recomputed after a child's
    /// content changed.
    /// </summary>
    void ClearCache(HepNodeVertex vertex)
    {
        vertex.CurrentNode.RecomputeDigest();
        vertex.RecomputeDigest();

        var seen = new HashSet<HepNodeVertex>();
        var queue = new Queue<DefaultEdge>(_graph.GetInwardEdges(vertex));
        while (queue.Count > 0)
        {
            var source = (HepNodeVertex)queue.Dequeue().Source;
            if (!seen.Add(source))
                continue;

            source.CurrentNode.RecomputeDigest();
            source.RecomputeDigest();
            foreach (var edge in _graph.GetInwardEdges(source))
                queue.Enqueue(edge);
        }
    }

    void UpdateVertex(HepNodeVertex vertex, INode rel)
    {
        if (!ReferenceEquals(rel, vertex.CurrentNode))
            FireNodeDiscarded(vertex.CurrentNode);

        var oldKey = vertex.CurrentNode.GetDigest();
        if (_mapDigestToVertex.TryGetValue(oldKey, out var mapped) && ReferenceEquals(mapped, vertex))
            _mapDigestToVertex.Remove(oldKey);

        _mapDigestToVertex[rel.GetDigest()] = vertex;
        if (!ReferenceEquals(rel, vertex.CurrentNode))
            vertex.ReplaceNode(rel);

        FireNodeEquivalenceFound(rel);
    }

    INode BuildFinalPlan(HepNodeVertex vertex, Dictionary<HepNodeVertex, INode> memo)
    {
        if (memo.TryGetValue(vertex, out var done))
            return done;

        var rel = vertex.CurrentNode;
        FireNodeChosen(rel);

        var children = rel.Children;
        ImmutableArray<INode>.Builder? builder = null;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] is HepNodeVertex childVertex)
            {
                builder ??= children.ToBuilder();
                builder[i] = BuildFinalPlan(childVertex, memo);
            }
        }

        var result = builder is null ? rel : rel.Copy(rel.Traits, builder.ToImmutable());
        memo[vertex] = result;
        return result;
    }

    // ~ Garbage collection ---------------------------------------------------

    void TryCleanVertices(HepNodeVertex vertex)
    {
        if (ReferenceEquals(vertex, _root) || !_graph.VertexSet.Contains(vertex) || _graph.GetInwardEdges(vertex).Count != 0)
            return;

        var rel = vertex.CurrentNode;
        FireNodeDiscarded(rel);

        var outVertices = new List<HepNodeVertex>();
        foreach (var edge in _graph.GetOutwardEdges(vertex))
            outVertices.Add((HepNodeVertex)edge.Target);

        foreach (var child in outVertices)
            _graph.RemoveEdge(vertex, child);

        _graph.RemoveAllVertices(new[] { vertex });
        _mapDigestToVertex.Remove(rel.GetDigest());

        foreach (var child in outVertices)
            TryCleanVertices(child);

        if (_enableFiredRulesCache)
            RemoveFiredRules(rel);
    }

    void CollectGarbage(HashSet<HepNodeVertex> garbage)
    {
        foreach (var vertex in garbage)
            TryCleanVertices(vertex);
    }

    void CollectGarbage()
    {
        if (_nTransformations == _nTransformationsLastGC)
            return; // Nothing has changed, so there is no garbage.

        _nTransformationsLastGC = _nTransformations;

        // Basic mark-and-sweep.
        var rootSet = new HashSet<HepNodeVertex>();
        var root = _root ?? throw new InvalidOperationException("root");
        if (_graph.VertexSet.Contains(root))
            BreadthFirstIterator<HepNodeVertex, DefaultEdge>.Reachable(rootSet, _graph, root);

        if (rootSet.Count == _graph.VertexSet.Count)
            return; // Everything is reachable: no garbage.

        var sweepSet = new HashSet<HepNodeVertex>();
        foreach (var vertex in _graph.VertexSet)
        {
            if (!rootSet.Contains(vertex))
            {
                sweepSet.Add(vertex);
                FireNodeDiscarded(vertex.CurrentNode);
            }
        }

        _graph.RemoveAllVertices(sweepSet);
        _graphSizeLastGC = _graph.VertexSet.Count;

        foreach (var vertex in sweepSet)
        {
            var key = vertex.CurrentNode.GetDigest();
            if (_mapDigestToVertex.TryGetValue(key, out var mapped) && sweepSet.Contains(mapped))
                _mapDigestToVertex.Remove(key);
        }

        if (_enableFiredRulesCache)
            foreach (var vertex in sweepSet)
                RemoveFiredRules(vertex.CurrentNode);
    }

    void RemoveFiredRules(INode rel)
    {
        if (!_firedRulesCacheIndex.TryGetValue(rel, out var keys))
            return;

        foreach (var key in keys)
            _firedRulesCache.Remove(key);

        _firedRulesCacheIndex.Remove(rel);
    }

    ICost GetCost(INode node)
    {
        var current = node is HepNodeVertex vertex ? vertex.CurrentNode : node;
        var cost = current.ComputeSelfCost(this);
        foreach (var child in current.Children)
            cost = cost.Plus(GetCost(child));

        return cost;
    }

    // ~ RuleOperand matching (sees through vertices) -----------------------------

    static ImmutableArray<INode>? Match(RuleOperand operand, INode node)
    {
        var bound = new List<INode>();
        return MatchOperand(operand, node, bound) ? bound.ToImmutableArray() : null;
    }

    static bool MatchOperand(RuleOperand operand, INode node, List<INode> bound)
    {
        while (node is HepNodeVertex vertex)
            node = vertex.CurrentNode;

        if (!operand.Matches(node))
            return false;

        switch (operand.ChildPolicy)
        {
            case RuleOperandChildPolicy.Any:
                bound.Add(node);
                return true;

            case RuleOperandChildPolicy.Leaf:
                if (!node.Children.IsEmpty)
                    return false;
                bound.Add(node);
                return true;

            case RuleOperandChildPolicy.Some:
                if (node.Children.Length != operand.Children.Length)
                    return false;
                bound.Add(node);
                for (int i = 0; i < operand.Children.Length; i++)
                    if (!MatchOperand(operand.Children[i], node.Children[i], bound))
                        return false;
                return true;

            case RuleOperandChildPolicy.Unordered:
                if (node.Children.Length != operand.Children.Length)
                    return false;
                bound.Add(node);
                return MatchUnordered(operand.Children, node.Children, new bool[node.Children.Length], 0, bound);

            default:
                return false;
        }
    }

    static bool MatchUnordered(ImmutableArray<RuleOperand> operands, ImmutableArray<INode> children, bool[] used, int index, List<INode> bound)
    {
        if (index == operands.Length)
            return true;

        for (int c = 0; c < children.Length; c++)
        {
            if (used[c])
                continue;

            var mark = bound.Count;
            if (MatchOperand(operands[index], children[c], bound))
            {
                used[c] = true;
                if (MatchUnordered(operands, children, used, index + 1, bound))
                    return true;
                used[c] = false;
            }

            bound.RemoveRange(mark, bound.Count - mark);
        }

        return false;
    }

    /// <summary>
    /// The key for the fired-rules cache: the identity of the matched nodes, in order.
    /// </summary>
    sealed class FiredKey : IEquatable<FiredKey>
    {

        readonly ImmutableArray<INode> _nodes;
        readonly int _hash;

        public FiredKey(ImmutableArray<INode> nodes)
        {
            _nodes = nodes;
            var hash = new HashCode();
            foreach (var node in nodes)
                hash.Add(node, ReferenceEqualityComparer.Instance);
            _hash = hash.ToHashCode();
        }

        public bool Equals(FiredKey? other)
        {
            if (other is null || other._nodes.Length != _nodes.Length)
                return false;

            for (int i = 0; i < _nodes.Length; i++)
                if (!ReferenceEquals(_nodes[i], other._nodes[i]))
                    return false;

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as FiredKey);

        public override int GetHashCode() => _hash;

    }

}
