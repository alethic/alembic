using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using Alembic.Algebra;
using Alembic.Algebra.Convert;
using Alembic.Util;
using Alembic.Util.Graph;

namespace Alembic.Plan.Hep;

/// <summary>
/// A heuristic planner: it applies its program's rules to a shared graph of the plan, in the configured
/// order, until no rule changes anything. Deterministic and program-driven.
/// </summary>
/// <remarks>
/// The plan is a single-rooted DAG of <see cref="HepOpVertex"/>: equal subexpressions are interned to
/// one vertex, so a rule fires once per distinct subexpression and a rewrite is shared by every parent.
/// A rewrite replaces a vertex's content and re-points its parents; discarded vertices are reclaimed by
/// mark-and-sweep garbage collection.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner")]
public class HepPlanner : AbstractOpPlanner
{

    readonly HepProgram _mainProgram;
    readonly Dictionary<IOpDigest, HepOpVertex> _mapDigestToVertex = new Dictionary<IOpDigest, HepOpVertex>();
    readonly DirectedGraph<HepOpVertex, DefaultEdge> _graph = DefaultDirectedGraph<HepOpVertex, DefaultEdge>.Create();
    readonly Multimap<ImmutableIntList, OpRule> _firedRulesCache = new Multimap<ImmutableIntList, OpRule>();
    readonly Multimap<int, ImmutableIntList> _firedRulesCacheIndex = new Multimap<int, ImmutableIntList>();

    HepOpVertex? _root;
    IOp? _rootOp;
    OpTraitSet? _requestedRootTraits;
    int _nTransformations;
    int _graphSizeLastGC;
    int _nTransformationsLastGC;
    bool _noDag;
    bool _enableFiredRulesCache;
    bool _largePlanMode;

    /// <summary>
    /// Creates a planner driven by the given program. More rules can be added with
    /// <see cref="AbstractOpPlanner.AddRule"/> (e.g. by a convention's <see cref="IOpTrait.Register"/>).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "HepPlanner(HepProgram, Context)")]
    public HepPlanner(HepProgram program, IContext? context = null)
        : base(OpCost.Factory, context)
    {
        _mainProgram = program;
    }

    /// <summary>
    /// Creates a planner driven by the given program, costing ops with <paramref name="costFactory"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "HepPlanner(HepProgram, Context, boolean, Function2<RelNode, RelNode, Void>, RelOptCostFactory)")]
    public HepPlanner(HepProgram program, IOpCostFactory costFactory, IContext? context = null)
        : base(costFactory, context)
    {
        _mainProgram = program;
    }

    /// <summary>
    /// Creates a planner with an empty program, in large-plan mode with the fired-rules cache on, for
    /// running several programs in succession over a preserved graph (multi-phase optimization).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "HepPlanner()")]
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
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "isLargePlanMode()")]
    public bool LargePlanMode
    {
        get => _largePlanMode;
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "setLargePlanMode(boolean)")]
        set => _largePlanMode = value;
    }

    /// <summary>
    /// Enables the fired-rules cache: a rule will not fire twice on the same match.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "setEnableFiredRulesCache(boolean)")]
    public void SetEnableFiredRulesCache(bool enable)
    {
        _enableFiredRulesCache = enable;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "setRoot(RelNode)")]
    public override void SetRoot(IOp op)
    {
        _rootOp = op;

        // initOpToVertexCache quickly skips common (shared) nodes before traversing their inputs.
        var initOpToVertexCache = _largePlanMode && !_noDag
            ? new Dictionary<IOp, HepOpVertex>(ReferenceEqualityComparer.Instance)
            : null;

        _root = AddOpToGraph(op, initOpToVertexCache);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "getRoot()")]
    public override IOp? Root => _root;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "clear()")]
    public override void Clear()
    {
        base.Clear();
        ClearRules();
    }

    /// <summary>
    /// Removes all rules and the fired-rules caches, keeping the graph for reuse across programs.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "clearRules()")]
    public void ClearRules()
    {
        foreach (var rule in new List<OpRule>(Rules))
            RemoveRule(rule);

        _firedRulesCache.Clear();
        _firedRulesCacheIndex.Clear();
    }

    /// <summary>
    /// Ignores traits except for the root, where it remembers what the final conversion should be. The
    /// remembered request is consulted by <see cref="DoesConverterApply"/> so a converter may fire at the
    /// root to produce the requested traits (as Calcite does); HEP does not otherwise enforce them.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "changeTraits(RelNode, RelTraitSet)")]
    public override IOp ChangeTraits(IOp op, OpTraitSet toTraits)
    {
        if (ReferenceEquals(op, _rootOp) || (_root is not null && ReferenceEquals(op, _root.CurrentOp)))
            _requestedRootTraits = toTraits;

        return op;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "ensureRegistered(RelNode, RelNode)")]
    public override IOp EnsureRegistered(IOp op, IOp? equivalent) => op;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "findBestExp()")]
    public override IOp FindBestPlan()
    {
        if (_root is null)
            throw new InvalidOperationException("No root has been set.");

        ExecuteProgram(_mainProgram);

        // Get rid of everything except what's in the final plan.
        CollectGarbage();

        return BuildFinalPlan(_root, new Dictionary<HepOpVertex, IOp>());
    }

    // ~ Program execution ----------------------------------------------------

    /// <summary>
    /// Top-level entry point for a program: prepares its state and runs it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeProgram(HepProgram)")]
    internal void ExecuteProgram(HepProgram program)
    {
        var px = HepInstruction.PrepareContext.Create(this);
        var state = program.Prepare(px);
        state.Execute();
    }

    /// <summary>
    /// Runs each instruction of <paramref name="program"/> in turn, collecting garbage between
    /// instructions once enough has changed.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeProgram(HepProgram, HepProgram.State)")]
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

    /// <summary>
    /// Executes a <see cref="HepInstruction.MatchLimit"/> instruction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeMatchLimit(HepInstruction.MatchLimit, HepInstruction.MatchLimit.State)")]
    internal void ExecuteMatchLimit(HepInstruction.MatchLimit instruction, HepInstruction.MatchLimit.State state)
    {
        state.ProgramState!.MatchLimit = instruction.Limit;
    }

    /// <summary>
    /// Executes a <see cref="HepInstruction.MatchOrder"/> instruction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeMatchOrder(HepInstruction.MatchOrder, HepInstruction.MatchOrder.State)")]
    internal void ExecuteMatchOrder(HepInstruction.MatchOrder instruction, HepInstruction.MatchOrder.State state)
    {
        state.ProgramState!.MatchOrder = instruction.Order;
    }

    /// <summary>
    /// Executes a <see cref="HepInstruction.RuleInstance"/> instruction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeRuleInstance(HepInstruction.RuleInstance, HepInstruction.RuleInstance.State)")]
    internal void ExecuteRuleInstance(HepInstruction.RuleInstance instruction, HepInstruction.RuleInstance.State state)
    {
        if (state.ProgramState!.SkippingGroup())
            return;

        ApplyRules(state.ProgramState, new[] { instruction.Rule }, true);
    }

    /// <summary>
    /// Executes a <see cref="HepInstruction.RuleLookup"/> instruction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeRuleLookup(HepInstruction.RuleLookup, HepInstruction.RuleLookup.State)")]
    internal void ExecuteRuleLookup(HepInstruction.RuleLookup instruction, HepInstruction.RuleLookup.State state)
    {
        if (state.ProgramState!.SkippingGroup())
            return;

        state.Rule ??= GetRuleByDescription(instruction.RuleDescription);
        if (state.Rule is not null)
            ApplyRules(state.ProgramState, new[] { state.Rule }, true);
    }

    /// <summary>
    /// Executes a <see cref="HepInstruction.RuleClass"/> instruction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeRuleClass(HepInstruction.RuleClass, HepInstruction.RuleClass.State)")]
    internal void ExecuteRuleClass(HepInstruction.RuleClass instruction, HepInstruction.RuleClass.State state)
    {
        if (state.ProgramState!.SkippingGroup())
            return;

        if (state.RuleSet is null)
        {
            state.RuleSet = new LinkedHashSet<OpRule>();
            foreach (var rule in Rules)
                if (instruction.RuleType.IsInstanceOfType(rule))
                    state.RuleSet.Add(rule);
        }

        ApplyRules(state.ProgramState, state.RuleSet, true);
    }

    /// <summary>
    /// Executes a <see cref="HepInstruction.RuleCollection"/> instruction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeRuleCollection(HepInstruction.RuleCollection, HepInstruction.RuleCollection.State)")]
    internal void ExecuteRuleCollection(HepInstruction.RuleCollection instruction, HepInstruction.RuleCollection.State state)
    {
        if (state.ProgramState!.SkippingGroup())
            return;

        ApplyRules(state.ProgramState, instruction.Rules, true);
    }

    /// <summary>
    /// Executes a <see cref="HepInstruction.ConverterRules"/> instruction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeConverterRules(HepInstruction.ConverterRules, HepInstruction.ConverterRules.State)")]
    internal void ExecuteConverterRules(HepInstruction.ConverterRules instruction, HepInstruction.ConverterRules.State state)
    {
        if (state.RuleSet is null)
        {
            state.RuleSet = new LinkedHashSet<OpRule>();
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

    /// <summary>
    /// Executes a <see cref="HepInstruction.CommonOpSubExprRules"/> instruction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeCommonRelSubExprRules(HepInstruction.CommonRelSubExprRules, HepInstruction.CommonRelSubExprRules.State)")]
    internal void ExecuteCommonOpSubExprRules(HepInstruction.CommonOpSubExprRules instruction, HepInstruction.CommonOpSubExprRules.State state)
    {
        if (state.RuleSet is null)
        {
            state.RuleSet = new LinkedHashSet<OpRule>();
            foreach (var rule in Rules)
                if (rule is ICommonSubExprRule)
                    state.RuleSet.Add(rule);
        }

        ApplyRules(state.ProgramState!, state.RuleSet, true);
    }

    /// <summary>
    /// Executes a <see cref="HepInstruction.SubProgram"/> instruction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeSubProgram(HepInstruction.SubProgram, HepInstruction.SubProgram.State)")]
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

    /// <summary>
    /// Executes a <see cref="HepInstruction.BeginGroup"/> instruction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeBeginGroup(HepInstruction.BeginGroup, HepInstruction.BeginGroup.State)")]
    internal void ExecuteBeginGroup(HepInstruction.BeginGroup instruction, HepInstruction.BeginGroup.State state)
    {
        state.ProgramState!.Group = state.EndGroup;
    }

    /// <summary>
    /// Executes a <see cref="HepInstruction.EndGroup"/> instruction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "executeEndGroup(HepInstruction.EndGroup, HepInstruction.EndGroup.State)")]
    internal void ExecuteEndGroup(HepInstruction.EndGroup instruction, HepInstruction.EndGroup.State state)
    {
        state.ProgramState!.Group = null;
        state.Collecting = false;
        ApplyRules(state.ProgramState, state.RuleSet, true);
    }

    // ~ Rule application -----------------------------------------------------

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "depthFirstApply(HepProgram.State, Iterator<HepRelVertex>, Collection<RelOptRule>, boolean, int)")]
    int DepthFirstApply(HepProgram.State programState, IEnumerator<HepOpVertex> iter, IReadOnlyList<OpRule> rules, bool forceConversions, int nMatches)
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

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "applyRules(HepProgram.State, Collection<RelOptRule>, boolean)")]
    void ApplyRules(HepProgram.State programState, IEnumerable<OpRule> rules, bool forceConversions)
    {
        var group = programState.Group;
        if (group is not null)
        {
            foreach (var rule in rules)
                group.RuleSet.Add(rule);

            return;
        }

        var ruleList = rules as IReadOnlyList<OpRule> ?? new List<OpRule>(rules);
        var fullRestart = programState.MatchOrder != HepMatchOrder.Arbitrary && programState.MatchOrder != HepMatchOrder.DepthFirst;

        // In large-plan mode an unordered/depth-first pass uses a vertex iterator that can resume from a
        // new vertex rather than restarting from the root each time, avoiding revisiting stable subgraphs.
        var useHepVertexIterator = (programState.MatchOrder == HepMatchOrder.Arbitrary || programState.MatchOrder == HepMatchOrder.DepthFirst) && _largePlanMode;
        var nMatches = 0;

        bool fixedPoint;
        do
        {
            IEnumerator<HepOpVertex> iter = useHepVertexIterator
                ? new HepVertexIterator(_root!, new HashSet<int>())
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

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "getGraphIterator(HepProgram.State, HepRelVertex)")]
    IEnumerator<HepOpVertex> GetGraphIterator(HepProgram.State programState, HepOpVertex start)
    {
        switch (programState.MatchOrder)
        {
            case HepMatchOrder.Arbitrary:
            case HepMatchOrder.DepthFirst:
                if (_largePlanMode)
                    return new HepVertexIterator(start, new HashSet<int>());
                return new DepthFirstIterator<HepOpVertex, DefaultEdge>(_graph, start);

            case HepMatchOrder.TopDown:
            case HepMatchOrder.BottomUp:
                // tryCleanVertices already removes the vertices a transformation orphaned, so the
                // topological order is over the live graph.
                if (!_largePlanMode)
                    CollectGarbage();
                return new TopologicalOrderIterator<HepOpVertex, DefaultEdge>(_graph, programState.MatchOrder);

            default:
                throw new NotSupportedException("Unsupported match order: " + programState.MatchOrder);
        }
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "applyRule(RelOptRule, HepRelVertex, boolean)")]
    HepOpVertex? ApplyRule(OpRule rule, HepOpVertex vertex, bool forceConversions)
    {
        if (!_graph.VertexSet.Contains(vertex))
            return null;

        IOpTrait? parentTrait = null;
        List<IOp>? parents = null;
        if (rule is ConverterRule converter)
        {
            // Guaranteed converter rules require special casing to make sure they only fire where
            // actually needed, otherwise they tend to fire to infinity and beyond.
            if (converter.IsGuaranteed || !forceConversions)
            {
                if (!DoesConverterApply(converter, vertex))
                    return null;

                parentTrait = converter.Target;
            }
        }
        else if (rule is ICommonSubExprRule)
        {
            // Only fire CommonRelSubExprRules if the vertex is a common subexpression.
            var parentVertices = GetVertexParents(vertex);
            if (parentVertices.Count < 2)
                return null;

            parents = new List<IOp>();
            foreach (var pVertex in parentVertices)
                parents.Add(pVertex.CurrentOp);
        }

        var bindings = new List<IOp>();
        var nodeChildren = new Dictionary<IOp, IReadOnlyList<IOp>>();
        if (!MatchOperand(rule.GetOperand(), vertex.CurrentOp, bindings, nodeChildren))
            return null;

        var boundOps = bindings.ToArray();

        // Cache the fired rule before constructing a HepRuleCall.
        ImmutableIntList? opIds = null;
        if (_enableFiredRulesCache)
        {
            var ids = new int[bindings.Count];
            for (int i = 0; i < bindings.Count; i++)
                ids[i] = bindings[i].Id;

            opIds = ImmutableIntList.Of(ids);
            if (_firedRulesCache.Get(opIds).Contains(rule))
                return null;
        }

        var call = new HepRuleCall(this, rule.GetOperand(), boundOps, nodeChildren, parents);

        // Let the rule apply its own side-condition.
        if (!rule.Matches(call))
            return null;

        FireRuleAttempted(call, true);
        rule.OnMatch(call);
        FireRuleAttempted(call, false);

        if (opIds is not null)
        {
            _firedRulesCache.Put(opIds, rule);
            for (int i = 0; i < opIds.Count; i++)
                _firedRulesCacheIndex.Put(opIds.GetInt(i), opIds);
        }

        if (call.Results.Count > 0)
            return ApplyTransformationResults(vertex, call, parentTrait);

        return null;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "doesConverterApply(ConverterRule, HepRelVertex)")]
    bool DoesConverterApply(ConverterRule converter, HepOpVertex vertex)
    {
        var outTrait = converter.Target;
        foreach (var parent in Graphs.PredecessorListOf(_graph, vertex))
        {
            var parentOp = parent.CurrentOp;
            if (parentOp is IConverter)
                continue; // We don't support converter chains.

            if (parentOp.Traits.Contains(outTrait))
                return true; // This parent wants the traits the converter produces.
        }

        return ReferenceEquals(vertex, _root)
            && _requestedRootTraits is not null
            && _requestedRootTraits.Contains(outTrait);
    }

    /// <summary>
    /// The parent vertices of a vertex, counted once per input reference.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "getVertexParents(HepRelVertex)")]
    List<HepOpVertex> GetVertexParents(HepOpVertex vertex)
    {
        var parents = new List<HepOpVertex>();
        foreach (var parentVertex in Graphs.PredecessorListOf(_graph, vertex))
        {
            var parent = parentVertex.CurrentOp;
            foreach (var child in parent.Children)
                if (ReferenceEquals(child, vertex))
                    parents.Add(parentVertex);
        }

        return parents;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "applyTransformationResults(HepRelVertex, HepRuleCall, RelTrait)")]
    HepOpVertex ApplyTransformationResults(HepOpVertex vertex, HepRuleCall call, IOpTrait? parentTrait)
    {
        IOp? bestOp;
        if (call.Results.Count == 1)
        {
            bestOp = call.Results[0];
        }
        else
        {
            bestOp = null;
            IOpCost? bestCost = null;
            var mq = call.GetMetadataQuery();
            foreach (var rel in call.Results)
            {
                var thisCost = GetCost(rel, mq);
                if (thisCost is null)
                    continue;

                if (bestOp is null || thisCost.IsLessThan(bestCost!))
                {
                    bestOp = rel;
                    bestCost = thisCost;
                }
            }
        }

        ++_nTransformations;
        FireRuleProductionSucceeded(call, bestOp!, before: true);

        // Snapshot the parents before adding the result, so contraction only re-points existing parents,
        // not new ones (which would create loops). Filter by trait when this is a converter rule.
        var parents = new List<HepOpVertex>();
        foreach (var parent in Graphs.PredecessorListOf(_graph, vertex))
        {
            if (parentTrait is not null)
            {
                var parentRel = parent.CurrentOp;
                if (parentRel is IConverter)
                    continue;

                if (!parentRel.Traits.Contains(parentTrait))
                    continue; // This parent does not want the converted result.
            }

            parents.Add(parent);
        }

        var newVertex = AddOpToGraph(bestOp!, null);
        var garbage = new LinkedHashSet<HepOpVertex>();

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

        FireRuleProductionSucceeded(call, bestOp!, before: false);

        return newVertex;
    }

    static bool ShallowEqual(ImmutableArray<IOp> a, IReadOnlyList<IOp> b)
    {
        if (a.Length != b.Count)
            return false;

        for (int i = 0; i < a.Length; i++)
            if (!ReferenceEquals(a[i], b[i]))
                return false;

        return true;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "addRelToGraph(RelNode, IdentityHashMap<RelNode, HepRelVertex>)")]
    HepOpVertex AddOpToGraph(IOp op, Dictionary<IOp, HepOpVertex>? initOpToVertexCache)
    {
        if (op is HepOpVertex existing && _graph.VertexSet.Contains(existing))
            return existing;

        // Fast equiv vertex for set-root, before traversing children (skips shared subexpressions).
        if (initOpToVertexCache is not null && initOpToVertexCache.TryGetValue(op, out var cached))
            return cached;

        // Recursively add children, replacing this op's inputs with their vertices.
        var newInputs = new List<IOp>(op.Children.Length);
        foreach (var input in op.Children)
            newInputs.Add(AddOpToGraph(input, initOpToVertexCache));

        if (!ShallowEqual(op.Children, newInputs))
            op = op.Copy(op.Traits, newInputs.ToImmutableArray());

        // Compute the digest the first time we add to the DAG, otherwise a common subexpression can't be
        // found for the equivalent-vertex lookup below.
        op.RecomputeDigest();

        if (!_noDag && _mapDigestToVertex.TryGetValue(op.GetOpDigest(), out var equivVertex))
            return equivVertex; // Use the existing equivalent vertex.

        var newVertex = new HepOpVertex(op);
        _graph.AddVertex(newVertex);
        UpdateVertex(newVertex, op);

        foreach (var input in op.Children)
            _graph.AddEdge(newVertex, (HepOpVertex)input);

        if (initOpToVertexCache is not null)
            initOpToVertexCache[op] = newVertex;

        _nTransformations++;
        return newVertex;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "contractVertices(HepRelVertex, HepRelVertex, List<HepRelVertex>, Set<HepRelVertex>)")]
    void ContractVertices(HepOpVertex preservedVertex, HepOpVertex discardedVertex, List<HepOpVertex> parents, LinkedHashSet<HepOpVertex> garbage)
    {
        if (ReferenceEquals(preservedVertex, discardedVertex))
            return;

        var op = preservedVertex.CurrentOp;
        UpdateVertex(preservedVertex, op);

        // Update specified parents of discardedVertex.
        foreach (var parent in parents)
        {
            var parentOp = parent.CurrentOp;
            var inputs = parentOp.Children;
            for (int i = 0; i < inputs.Length; i++)
            {
                var child = inputs[i];
                if (!ReferenceEquals(child, discardedVertex))
                    continue;

                parentOp.ReplaceInput(i, preservedVertex);
            }

            ClearCache(parent);
            _graph.RemoveEdge(parent, discardedVertex);

            if (!_noDag && _largePlanMode)
            {
                // Recursive merge parent path
                if (_mapDigestToVertex.TryGetValue(parentOp.GetOpDigest(), out var addedVertex)
                    && !ReferenceEquals(addedVertex, parent))
                {
                    // contractVertices will change predecessorList
                    var parentCopy = new List<HepOpVertex>(Graphs.PredecessorListOf(_graph, parent));
                    ContractVertices(addedVertex, parent, parentCopy, garbage);
                    continue;
                }
            }

            _graph.AddEdge(parent, preservedVertex);
            UpdateVertex(parent, parentOp);
        }

        // We don't remove discardedVertex now (it may still be reachable from preservedVertex); garbage
        // collection takes care of it.
        if (ReferenceEquals(discardedVertex, _root))
            _root = preservedVertex;

        garbage.Add(discardedVertex);
    }

    /// <summary>
    /// Clears the metadata cache for the op of a vertex and its ancestors, after a child's content changed.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "clearCache(HepRelVertex)")]
    void ClearCache(HepOpVertex vertex)
    {
        ClearMetadataCache(vertex.CurrentOp);
        if (!ClearMetadataCache(vertex))
            return;

        var queue = new Queue<DefaultEdge>(_graph.GetInwardEdges(vertex));
        while (queue.Count > 0)
        {
            var source = (HepOpVertex)queue.Dequeue().Source;
            ClearMetadataCache(source.CurrentOp);
            if (ClearMetadataCache(source))
                foreach (var edge in _graph.GetInwardEdges(source))
                    queue.Enqueue(edge);
        }
    }

    /// <summary>
    /// Clears the metadata cache for <paramref name="op"/>, returning whether anything was cached.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.RelMdUtil", "clearCache(RelNode)")]
    static bool ClearMetadataCache(IOp op) => op.Cluster.GetMetadataQuery().ClearCache(op);

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "updateVertex(HepRelVertex, RelNode)")]
    void UpdateVertex(HepOpVertex vertex, IOp op)
    {
        if (!ReferenceEquals(op, vertex.CurrentOp))
            FireOpDiscarded(vertex.CurrentOp);

        var oldKey = vertex.CurrentOp.GetOpDigest();
        if (_mapDigestToVertex.TryGetValue(oldKey, out var mapped) && ReferenceEquals(mapped, vertex))
            _mapDigestToVertex.Remove(oldKey);

        _mapDigestToVertex[op.GetOpDigest()] = vertex;
        if (!ReferenceEquals(op, vertex.CurrentOp))
            vertex.ReplaceOp(op);
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "buildFinalPlan(HepRelVertex)")]
    IOp BuildFinalPlan(HepOpVertex vertex, Dictionary<HepOpVertex, IOp> memo)
    {
        if (memo.TryGetValue(vertex, out var done))
            return done;

        var op = vertex.CurrentOp;
        FireOpChosen(op);

        var children = op.Children;
        ImmutableArray<IOp>.Builder? builder = null;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] is HepOpVertex childVertex)
            {
                builder ??= children.ToBuilder();
                builder[i] = BuildFinalPlan(childVertex, memo);
            }
        }

        var result = builder is null ? op : op.Copy(op.Traits, builder.ToImmutable());
        memo[vertex] = result;
        return result;
    }

    // ~ Garbage collection ---------------------------------------------------

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "tryCleanVertices(HepRelVertex)")]
    void TryCleanVertices(HepOpVertex vertex)
    {
        if (ReferenceEquals(vertex, _root) || !_graph.VertexSet.Contains(vertex) || _graph.GetInwardEdges(vertex).Count != 0)
            return;

        var op = vertex.CurrentOp;
        FireOpDiscarded(op);

        var outVertices = new LinkedHashSet<HepOpVertex>();
        foreach (var edge in _graph.GetOutwardEdges(vertex))
            outVertices.Add((HepOpVertex)edge.Target);

        foreach (var child in outVertices)
            _graph.RemoveEdge(vertex, child);

        Debug.Assert(_graph.GetInwardEdges(vertex).Count == 0);
        Debug.Assert(_graph.GetOutwardEdges(vertex).Count == 0);
        _graph.RemoveAllVertices(new[] { vertex });
        _mapDigestToVertex.Remove(op.GetOpDigest());

        foreach (var child in outVertices)
            TryCleanVertices(child);

        ClearCache(vertex);

        if (_enableFiredRulesCache)
            foreach (var opIds in _firedRulesCacheIndex.Get(op.Id))
                _firedRulesCache.RemoveAll(opIds);
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "collectGarbage(Set<HepRelVertex>)")]
    void CollectGarbage(LinkedHashSet<HepOpVertex> garbage)
    {
        foreach (var vertex in garbage)
            TryCleanVertices(vertex);
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "collectGarbage()")]
    void CollectGarbage()
    {
        if (_nTransformations == _nTransformationsLastGC)
            return; // Nothing has changed, so there is no garbage.

        _nTransformationsLastGC = _nTransformations;

        // Basic mark-and-sweep.
        var rootSet = new HashSet<HepOpVertex>();
        var root = _root ?? throw new InvalidOperationException("root");
        if (_graph.VertexSet.Contains(root))
            BreadthFirstIterator<HepOpVertex, DefaultEdge>.Reachable(rootSet, _graph, root);

        if (rootSet.Count == _graph.VertexSet.Count)
            return; // Everything is reachable: no garbage.

        var sweepSet = new HashSet<HepOpVertex>();
        foreach (var vertex in _graph.VertexSet)
        {
            if (!rootSet.Contains(vertex))
            {
                sweepSet.Add(vertex);
                FireOpDiscarded(vertex.CurrentOp);
            }
        }

        _graph.RemoveAllVertices(sweepSet);
        _graphSizeLastGC = _graph.VertexSet.Count;

        foreach (var vertex in sweepSet)
        {
            var key = vertex.CurrentOp.GetOpDigest();
            if (_mapDigestToVertex.TryGetValue(key, out var mapped) && sweepSet.Contains(mapped))
                _mapDigestToVertex.Remove(key);
        }

        if (_enableFiredRulesCache)
            foreach (var vertex in sweepSet)
            {
                foreach (var opIds in _firedRulesCacheIndex.Get(vertex.CurrentOp.Id))
                    _firedRulesCache.RemoveAll(opIds);

                _firedRulesCacheIndex.RemoveAll(vertex.CurrentOp.Id);
            }
    }


    // ~ RuleOperand matching (sees through vertices) -----------------------------

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepPlanner", "matchOperands(RelOptRuleOperand, RelNode, List<RelNode>, Map<RelNode, List<RelNode>>)")]
    static bool MatchOperand(OpRuleOperand operand, IOp op, List<IOp> bindings, Dictionary<IOp, IReadOnlyList<IOp>> nodeChildren)
    {
        if (!operand.Matches(op))
            return false;

        foreach (var input in op.Children)
            if (input is not HepOpVertex)
                // The graph could be partially optimized (e.g. for a materialized view). In that case the
                // input is a real op, not a vertex, and should not be matched again here.
                return false;

        bindings.Add(op);
        var childOps = op.Children;

        switch (operand.ChildPolicy)
        {
            case RuleOperandChildPolicy.Any:
                return true;

            case RuleOperandChildPolicy.Unordered:
                // For each operand, at least one child must match. If matchAnyChildren, usually there's
                // just one operand.
                foreach (var childOperand in operand.Children)
                {
                    var match = false;
                    foreach (var childOp in childOps)
                    {
                        match = MatchOperand(childOperand, ((HepOpVertex)childOp).CurrentOp, bindings, nodeChildren);
                        if (match)
                            break;
                    }

                    if (!match)
                        return false;
                }

                var children = new List<IOp>(childOps.Length);
                foreach (var childOp in childOps)
                    children.Add(((HepOpVertex)childOp).CurrentOp);

                nodeChildren[op] = children;
                return true;

            default:
                int n = operand.Children.Length;
                if (childOps.Length < n)
                    return false;

                for (int i = 0; i < n; i++)
                    if (!MatchOperand(operand.Children[i], ((HepOpVertex)childOps[i]).CurrentOp, bindings, nodeChildren))
                        return false;

                return true;
        }
    }

}
