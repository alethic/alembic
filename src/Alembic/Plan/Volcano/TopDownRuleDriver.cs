using System;
using System.Collections.Generic;

using Alembic.Algebra;
using Alembic.Algebra.Convert;

namespace Alembic.Plan.Volcano;

/// <summary>
/// A rule driver that applies rules top-down (the Cascades strategy). Optimization is expressed as a
/// stack of tasks: a task either applies rules or schedules further tasks, so the planner optimizes a
/// group by exploring its logical members, implementing them, optimizing inputs against an upper bound,
/// and deriving traits up from optimized inputs.
/// </summary>
/// <remarks>
/// The branch-and-bound structure is faithful, and lower-bound pruning is active:
/// <see cref="VolcanoPlanner.GetLowerBound"/> consults the metadata subsystem
/// (<see cref="Alembic.Algebra.Metadata.BuiltInMetadata.LowerBoundCost"/>), so the bound checks prune.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver")]
internal class TopDownRuleDriver : IRuleDriver
{

    readonly VolcanoPlanner _planner;
    readonly TopDownRuleQueue _ruleQueue;
    readonly Stack<ITask> _tasks = new Stack<ITask>();
    readonly HashSet<IOp> _passThroughCache = new HashSet<IOp>(ReferenceEqualityComparer.Instance);

    IGeneratorTask? _applying;

    /// <summary>
    /// Creates a driver for the given planner.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver", "TopDownRuleDriver(VolcanoPlanner)")]
    public TopDownRuleDriver(VolcanoPlanner planner)
    {
        _planner = planner;
        _ruleQueue = new TopDownRuleQueue(planner);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver", "getRuleQueue()")]
    public RuleQueue Queue => _ruleQueue;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver", "drive()")]
    public void Drive()
    {
        var root = _planner.RootSubset ?? throw new InvalidOperationException("No root has been set.");
        _tasks.Push(new OptimizeGroup(this, root, _planner.InfiniteCost));

        try
        {
            // Iterate until the root is fully optimized.
            while (_tasks.Count > 0)
                _tasks.Pop().Perform();
        }
        catch (VolcanoTimeoutException)
        {
            // Planning timed out; cancel the subsequent optimization, keeping the best plan so far.
        }
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver", "clear()")]
    public void Clear()
    {
        _ruleQueue.Clear();
        _tasks.Clear();
        _passThroughCache.Clear();
        _applying = null;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver", "applyGenerator(GeneratorTask, Procedure)")]
    void ApplyGenerator(IGeneratorTask? task, Action proc)
    {
        var applying = _applying;
        _applying = task;
        try
        {
            proc();
        }
        finally
        {
            _applying = applying;
        }
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver", "onSetMerged(RelSet)")]
    public void OnSetMerged(OpSet set)
    {
        // Merging may open new opportunities for an optimized group; clear the optimized state of the
        // set's subsets and their ancestors so they are optimized again.
        ApplyGenerator(null, () => ClearProcessed(set));
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver", "clearProcessed(RelSet)")]
    void ClearProcessed(OpSet set)
    {
        bool explored = set.Exploring is not null;
        set.Exploring = null;

        foreach (var subset in set.Subsets)
        {
            if (subset.ResetTaskState() || explored)
            {
                foreach (var parentRel in subset.GetParentOps())
                    ClearProcessed(_planner.GetSet(parentRel));

                if (ReferenceEquals(subset, _planner.RootSubset))
                    _tasks.Push(new OptimizeGroup(this, subset, _planner.InfiniteCost));
            }
        }
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver", "onProduce(RelNode, RelSubset)")]
    public void OnProduce(IOp op, OpSubset subset)
    {
        // If the op was added to an unrelated subset, ignore it; a later OptimizeGroup will schedule it.
        if (_applying is null || subset.Set != VolcanoPlanner.EquivRoot(_applying.Group.Set))
            return;

        if (!_applying.OnProduce(op))
            return;

        if (!_planner.IsLogical(op))
        {
            // A physical op: schedule tasks to optimize its inputs for the optimizing subset(s).
            OpSubset? optimizingGroup = null;
            bool canPassThrough = op is IPhysicalOp && !_passThroughCache.Contains(op);
            if (!canPassThrough && subset.TaskState is not null)
            {
                optimizingGroup = subset;
            }
            else
            {
                var upperBound = _planner.ZeroCost;
                var set = subset.Set;
                var subsetsToPassThrough = new List<OpSubset>();
                foreach (var otherSubset in set.Subsets)
                {
                    if (!otherSubset.IsRequired
                        || !ReferenceEquals(otherSubset, _planner.RootSubset)
                        && otherSubset.TaskState != OpSubset.OptimizeState.Optimizing)
                    {
                        continue;
                    }

                    if (op.Traits.Satisfies(otherSubset.Traits))
                    {
                        if (upperBound.IsLessThan(otherSubset.UpperBound))
                        {
                            upperBound = otherSubset.UpperBound;
                            optimizingGroup = otherSubset;
                        }
                    }
                    else if (canPassThrough)
                    {
                        subsetsToPassThrough.Add(otherSubset);
                    }
                }

                foreach (var otherSubset in subsetsToPassThrough)
                {
                    var task = GetOptimizeInputTask(op, otherSubset);
                    if (task is not null)
                        _tasks.Push(task);
                }
            }

            if (optimizingGroup is null)
                return;

            var optimizeTask = GetOptimizeInputTask(op, optimizingGroup);
            if (optimizeTask is not null)
                _tasks.Push(optimizeTask);
        }
        else
        {
            bool optimizing = false;
            foreach (var s in subset.Set.Subsets)
            {
                if (s.TaskState == OpSubset.OptimizeState.Optimizing)
                {
                    optimizing = true;
                    break;
                }
            }

            var applying = _applying;
            _tasks.Push(new OptimizeMExpr(this, op, applying.Group, applying.Exploring && !optimizing));
        }
    }

    /// <summary>
    /// Decides how to optimize a physical op for a target subset.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver", "getOptimizeInputTask(RelNode, RelSubset)")]
    ITask? GetOptimizeInputTask(IOp rel, OpSubset group)
    {
        // If the op does not deliver the group's traits, first try to convert it (pass-through or a
        // converter rule).
        if (!rel.Traits.Satisfies(group.Traits))
        {
            var passThroughRel = Convert(rel, group);
            if (passThroughRel is null)
                return null;

            var finalPassThroughRel = passThroughRel;
            ApplyGenerator(null, () => _planner.Register(finalPassThroughRel, group));
            rel = passThroughRel;
        }

        bool unProcessed = false;
        foreach (var input in rel.Inputs)
        {
            if (((OpSubset)input).GetWinnerCost() is null)
            {
                unProcessed = true;
                break;
            }
        }

        // If all inputs are optimized, only trait derivation remains.
        if (!unProcessed)
            return new DeriveTrait(this, rel, group);

        if (rel.Inputs.Length == 1)
            return new OptimizeInput1(this, rel, group);

        return new OptimizeInputs(this, rel, group);
    }

    /// <summary>
    /// Tries to convert a physical op to a target subset's traits, by trait pass-through or, failing
    /// that, by queuing a converter-rule match.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver", "convert(RelNode, RelSubset)")]
    IOp? Convert(IOp rel, OpSubset group)
    {
        if (!_passThroughCache.Contains(rel))
        {
            if (CheckLowerBound(rel, group))
            {
                var passThrough = group.PassThrough(rel);
                if (passThrough is not null)
                {
                    _passThroughCache.Add(passThrough);
                    return passThrough;
                }
            }
        }

        var match = _ruleQueue.PopMatch(rel, m =>
            m.Rule is ConverterRule converter && converter.Target.Satisfies(group.Traits.Convention));
        if (match is not null)
            _tasks.Push(new ApplyRule(this, match, group, false));

        return null;
    }

    /// <summary>
    /// Whether an op's lower bound is below a subset's upper bound (so optimizing it is worthwhile).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver", "checkLowerBound(RelNode, RelSubset)")]
    bool CheckLowerBound(IOp rel, OpSubset group)
    {
        var upperBound = group.UpperBound;
        if (upperBound.IsInfinite)
            return true;

        var lowerBound = _planner.GetLowerBound(rel);
        return !upperBound.IsLessThanOrEqual(lowerBound);
    }

    // ~ Tasks ------------------------------------------------------------------

    /// <summary>
    /// One unit of work on the driver's task stack.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.Task")]
    interface ITask
    {
        /// <summary>
        /// Runs the task, possibly pushing further tasks onto the driver's stack.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.Task", "perform()")]
        void Perform();
    }

    /// <summary>
    /// A task that generates new ops into a group; it is notified of each op produced by a rule.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.GeneratorTask")]
    interface IGeneratorTask : ITask
    {
        /// <summary>
        /// The group the produced ops belong to.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.GeneratorTask", "group()")]
        OpSubset Group { get; }

        /// <summary>
        /// Whether this task is exploring (rather than implementing) the group.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.GeneratorTask", "exploring()")]
        bool Exploring { get; }

        /// <summary>
        /// Called for each <paramref name="op"/> a rule produces; returns whether to keep it.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.GeneratorTask", "onProduce(RelNode)")]
        bool OnProduce(IOp op);
    }

    /// <summary>
    /// Optimizes a subset: schedules optimization for its members.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeGroup")]
    sealed class OptimizeGroup : ITask
    {
        readonly TopDownRuleDriver _driver;
        readonly OpSubset _group;
        readonly IOpCost _upperBound;

        /// <summary>
        /// Creates the task.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeGroup", "OptimizeGroup(RelSubset, RelOptCost)")]
        public OptimizeGroup(TopDownRuleDriver driver, OpSubset group, IOpCost upperBound)
        {
            _driver = driver;
            _group = group;
            _upperBound = upperBound;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeGroup", "perform()")]
        public void Perform()
        {
            if (_group.GetWinnerCost() is not null)
                return;

            if (_group.TaskState is not null && _upperBound.IsLessThanOrEqual(_group.UpperBound))
                return;

            _group.StartOptimize(_upperBound);

            _driver._tasks.Push(new GroupOptimized(_group));

            var physicals = new List<IOp>();
            foreach (var rel in _group.Set.Ops)
            {
                if (_driver._planner.IsLogical(rel))
                    _driver._tasks.Push(new OptimizeMExpr(_driver, rel, _group, false));
                else if (rel.IsEnforcer)
                    physicals.Insert(0, rel);
                else
                    physicals.Add(rel);
            }

            // Apply input optimization first so as to get a valid upper bound.
            foreach (var rel in physicals)
            {
                var task = _driver.GetOptimizeInputTask(rel, _group);
                if (task is not null)
                    _driver._tasks.Push(task);
            }
        }
    }

    /// <summary>
    /// Marks a subset optimized once its OptimizeGroup tasks complete.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.GroupOptimized")]
    sealed class GroupOptimized : ITask
    {
        readonly OpSubset _group;

        /// <summary>
        /// Creates the task that marks <paramref name="group"/> optimized.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.GroupOptimized", "GroupOptimized(RelSubset)")]
        public GroupOptimized(OpSubset group)
        {
            _group = group;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.GroupOptimized", "perform()")]
        public void Perform() => _group.SetOptimized();
    }

    /// <summary>
    /// Optimizes a logical op: explores its inputs, then applies rules to it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeMExpr")]
    sealed class OptimizeMExpr : ITask
    {
        readonly TopDownRuleDriver _driver;
        readonly IOp _mExpr;
        readonly OpSubset _group;
        readonly bool _explore;

        /// <summary>
        /// Creates the task.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeMExpr", "OptimizeMExpr(RelNode, RelSubset, boolean)")]
        public OptimizeMExpr(TopDownRuleDriver driver, IOp mExpr, OpSubset group, bool explore)
        {
            _driver = driver;
            _mExpr = mExpr;
            _group = group;
            _explore = explore;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeMExpr", "perform()")]
        public void Perform()
        {
            if (_explore && _group.IsExplored)
                return;

            _driver._tasks.Push(new ApplyRules(_driver, _mExpr, _group, _explore));
            for (int i = _mExpr.Inputs.Length - 1; i >= 0; --i)
                _driver._tasks.Push(new ExploreInput(_driver, _mExpr, i));
        }
    }

    /// <summary>
    /// Ensures an ExploreInput worked on the right input group (inputs may move when sets merge), then
    /// marks the input explored.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.EnsureGroupExplored")]
    sealed class EnsureGroupExplored : ITask
    {
        readonly TopDownRuleDriver _driver;
        readonly OpSubset _input;
        readonly IOp _parent;
        readonly int _inputOrdinal;

        /// <summary>
        /// Creates the task.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.EnsureGroupExplored", "EnsureGroupExplored(RelSubset, RelNode, int)")]
        public EnsureGroupExplored(TopDownRuleDriver driver, OpSubset input, IOp parent, int inputOrdinal)
        {
            _driver = driver;
            _input = input;
            _parent = parent;
            _inputOrdinal = inputOrdinal;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.EnsureGroupExplored", "perform()")]
        public void Perform()
        {
            if (!ReferenceEquals(_parent.Inputs[_inputOrdinal], _input))
            {
                _driver._tasks.Push(new ExploreInput(_driver, _parent, _inputOrdinal));
                return;
            }

            _input.SetExplored();
            foreach (var subset in _input.Set.Subsets)
            {
                // Clear the LB cache as exploring state has changed.
                _input.Cluster.GetMetadataQuery().ClearCache(subset);
            }
        }
    }

    /// <summary>
    /// Explores an input of an op: optimizes (for exploration) the logical members of the input group.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.ExploreInput")]
    sealed class ExploreInput : ITask
    {
        readonly TopDownRuleDriver _driver;
        readonly OpSubset _group;
        readonly IOp _parent;
        readonly int _inputOrdinal;

        /// <summary>
        /// Creates the task.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.ExploreInput", "ExploreInput(RelNode, int)")]
        public ExploreInput(TopDownRuleDriver driver, IOp parent, int inputOrdinal)
        {
            _driver = driver;
            _group = (OpSubset)parent.Inputs[inputOrdinal];
            _parent = parent;
            _inputOrdinal = inputOrdinal;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.ExploreInput", "perform()")]
        public void Perform()
        {
            if (!_group.Explore())
                return;

            _driver._tasks.Push(new EnsureGroupExplored(_driver, _group, _parent, _inputOrdinal));
            foreach (var rel in _group.Set.Ops)
            {
                if (_driver._planner.IsLogical(rel))
                    _driver._tasks.Push(new OptimizeMExpr(_driver, rel, _group, true));
            }
        }
    }

    /// <summary>
    /// Pulls matches for an op out of the queue and schedules an ApplyRule task for each.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.ApplyRules")]
    sealed class ApplyRules : ITask
    {
        readonly TopDownRuleDriver _driver;
        readonly IOp _mExpr;
        readonly OpSubset _group;
        readonly bool _exploring;

        /// <summary>
        /// Creates the task.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.ApplyRules", "ApplyRules(RelNode, RelSubset, boolean)")]
        public ApplyRules(TopDownRuleDriver driver, IOp mExpr, OpSubset group, bool exploring)
        {
            _driver = driver;
            _mExpr = mExpr;
            _group = group;
            _exploring = exploring;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.ApplyRules", "perform()")]
        public void Perform()
        {
            Func<VolcanoRuleMatch, bool>? predicate = _exploring ? _driver._planner.IsTransformationRule : null;
            var match = _driver._ruleQueue.PopMatch(_mExpr, predicate);
            while (match is not null)
            {
                _driver._tasks.Push(new ApplyRule(_driver, match, _group, _exploring));
                match = _driver._ruleQueue.PopMatch(_mExpr, predicate);
            }
        }
    }

    /// <summary>
    /// Applies a single rule match, with the driver tracking it as the current generator.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.ApplyRule")]
    sealed class ApplyRule : IGeneratorTask
    {
        readonly TopDownRuleDriver _driver;
        readonly VolcanoRuleMatch _match;
        readonly OpSubset _group;
        readonly bool _exploring;

        /// <summary>
        /// Creates the task.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.ApplyRule", "ApplyRule(VolcanoRuleMatch, RelSubset, boolean)")]
        public ApplyRule(TopDownRuleDriver driver, VolcanoRuleMatch match, OpSubset group, bool exploring)
        {
            _driver = driver;
            _match = match;
            _group = group;
            _exploring = exploring;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.ApplyRule", "group()")]
        public OpSubset Group => _group;

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.ApplyRule", "exploring()")]
        public bool Exploring => _exploring;

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.GeneratorTask", "onProduce(RelNode)")]
        public bool OnProduce(IOp op) => true;

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.ApplyRule", "perform()")]
        public void Perform() => _driver.ApplyGenerator(this, _match.OnMatch);
    }

    /// <summary>
    /// Optimizes the single input of a physical op, then derives traits.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeInput1")]
    sealed class OptimizeInput1 : ITask
    {
        readonly TopDownRuleDriver _driver;
        readonly IOp _mExpr;
        readonly OpSubset _group;

        /// <summary>
        /// Creates the task.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeInput1", "OptimizeInput1(RelNode, RelSubset)")]
        public OptimizeInput1(TopDownRuleDriver driver, IOp mExpr, OpSubset group)
        {
            _driver = driver;
            _mExpr = mExpr;
            _group = group;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeInput1", "perform()")]
        public void Perform()
        {
            var upperBound = _group.UpperBound;
            var upperForInput = _driver._planner.UpperBoundForInputs(_mExpr, upperBound);
            if (upperForInput.IsLessThanOrEqual(_driver._planner.ZeroCost))
                return;

            var input = (OpSubset)_mExpr.Inputs[0];

            _driver._tasks.Push(new DeriveTrait(_driver, _mExpr, _group));
            _driver._tasks.Push(new CheckInput(_driver, null, _mExpr, input, 0, upperForInput));
            _driver._tasks.Push(new OptimizeGroup(_driver, input, upperForInput));
        }
    }

    /// <summary>
    /// Optimizes a physical op's inputs, apportioning the upper bound across them, then derives traits.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeInputs")]
    sealed class OptimizeInputs : ITask
    {
        readonly TopDownRuleDriver _driver;
        readonly IOp _mExpr;
        readonly OpSubset _group;
        readonly int _childCount;
        IOpCost _upperBound;
        IOpCost _upperForInput;
        int _processingChild;

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeInputs", "lowerBounds")]
        internal List<IOpCost>? LowerBounds;

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeInputs", "lowerBoundSum")]
        internal IOpCost? LowerBoundSum;

        /// <summary>
        /// Creates the task.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeInputs", "OptimizeInputs(RelNode, RelSubset)")]
        public OptimizeInputs(TopDownRuleDriver driver, IOp rel, OpSubset group)
        {
            _driver = driver;
            _mExpr = rel;
            _group = group;
            _upperBound = group.UpperBound;
            _upperForInput = driver._planner.InfiniteCost;
            _childCount = rel.Inputs.Length;
            _processingChild = 0;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.OptimizeInputs", "perform()")]
        public void Perform()
        {
            var planner = _driver._planner;
            var bestCost = _group.BestCost;
            if (!bestCost.IsInfinite)
            {
                if (bestCost.IsLessThan(_upperBound))
                {
                    _upperBound = bestCost;
                    _upperForInput = planner.UpperBoundForInputs(_mExpr, _upperBound);
                }

                if (LowerBoundSum is null)
                {
                    if (_upperForInput.IsInfinite)
                        _upperForInput = planner.UpperBoundForInputs(_mExpr, _upperBound);

                    LowerBounds = new List<IOpCost>(_childCount);
                    foreach (var input in _mExpr.Inputs)
                    {
                        var lb = planner.GetLowerBound(input);
                        LowerBounds.Add(lb);
                        LowerBoundSum = LowerBoundSum is null ? lb : LowerBoundSum.Plus(lb);
                    }
                }

                if (_upperForInput.IsLessThan(LowerBoundSum!))
                    return;
            }

            if (LowerBoundSum is not null && LowerBoundSum.IsInfinite)
                return;

            if (_processingChild == 0)
                _driver._tasks.Push(new DeriveTrait(_driver, _mExpr, _group));

            while (_processingChild < _childCount)
            {
                var input = (OpSubset)_mExpr.Inputs[_processingChild];

                if (input.GetWinnerCost() is not null)
                {
                    ++_processingChild;
                    continue;
                }

                var upper = _upperForInput;
                if (!upper.IsInfinite)
                    upper = _upperForInput.Minus(LowerBoundSum!).Plus(LowerBounds![_processingChild]);

                if (input.TaskState is not null && upper.IsLessThanOrEqual(input.UpperBound))
                    return;

                if (_processingChild != _childCount - 1)
                    _driver._tasks.Push(this);

                _driver._tasks.Push(new CheckInput(_driver, this, _mExpr, input, _processingChild, upper));
                _driver._tasks.Push(new OptimizeGroup(_driver, input, upper));
                ++_processingChild;
                break;
            }
        }
    }

    /// <summary>
    /// After an input is optimized, verifies it and updates the parent's bound bookkeeping.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.CheckInput")]
    sealed class CheckInput : ITask
    {
        readonly TopDownRuleDriver _driver;
        readonly OptimizeInputs? _context;
        readonly IOpCost _upper;
        readonly IOp _parent;
        OpSubset _input;
        readonly int _i;

        /// <summary>
        /// Creates the task.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.CheckInput", "CheckInput(OptimizeInputs, RelNode, RelSubset, int, RelOptCost)")]
        public CheckInput(TopDownRuleDriver driver, OptimizeInputs? context, IOp parent, OpSubset input, int i, IOpCost upper)
        {
            _driver = driver;
            _context = context;
            _parent = parent;
            _input = input;
            _i = i;
            _upper = upper;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.CheckInput", "perform()")]
        public void Perform()
        {
            if (!ReferenceEquals(_input, _parent.Inputs[_i]))
            {
                _input = (OpSubset)_parent.Inputs[_i];
                _driver._tasks.Push(this);
                _driver._tasks.Push(new OptimizeGroup(_driver, _input, _upper));
                return;
            }

            if (_context is null)
                return;

            var winner = _input.GetWinnerCost();
            if (winner is null)
            {
                _context.LowerBoundSum = _driver._planner.InfiniteCost;
                return;
            }

            var lowerBoundSum = _context.LowerBoundSum;
            if (lowerBoundSum is not null && !lowerBoundSum.IsInfinite)
            {
                lowerBoundSum = lowerBoundSum.Minus(_context.LowerBounds![_i]);
                lowerBoundSum = lowerBoundSum.Plus(winner);
                _context.LowerBoundSum = lowerBoundSum;
                _context.LowerBounds![_i] = winner;
            }
        }
    }

    /// <summary>
    /// Derives traits for an optimized physical op from its inputs' delivered trait sets.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.DeriveTrait")]
    sealed class DeriveTrait : IGeneratorTask
    {
        readonly TopDownRuleDriver _driver;
        readonly IOp _mExpr;
        readonly OpSubset _group;

        /// <summary>
        /// Creates the task.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.DeriveTrait", "DeriveTrait(RelNode, RelSubset)")]
        public DeriveTrait(TopDownRuleDriver driver, IOp mExpr, OpSubset group)
        {
            _driver = driver;
            _mExpr = mExpr;
            _group = group;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.DeriveTrait", "group()")]
        public OpSubset Group => _group;

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.DeriveTrait", "exploring()")]
        public bool Exploring => false;

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.DeriveTrait", "onProduce(RelNode)")]
        public bool OnProduce(IOp op)
        {
            _driver._passThroughCache.Add(op);
            return true;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.DeriveTrait", "perform()")]
        public void Perform()
        {
            foreach (var input in _mExpr.Inputs)
            {
                if (((OpSubset)input).GetWinnerCost() is null)
                    return;
            }

            // In case some implementations convert between physical conventions via rules.
            _driver._tasks.Push(new ApplyRules(_driver, _mExpr, _group, false));

            if (!_driver._passThroughCache.Contains(_mExpr))
                _driver.ApplyGenerator(this, Derive);
        }

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.TopDownRuleDriver.DeriveTrait", "derive()")]
        void Derive()
        {
            if (_mExpr is not IPhysicalOp rel || rel.DeriveMode == DeriveMode.Prohibited)
                return;

            var mode = rel.DeriveMode;
            int arity = rel.Inputs.Length;
            var inputTraits = new List<IList<OpTraitSet>>(arity);

            for (int i = 0; i < arity; i++)
            {
                int childId = mode == DeriveMode.RightFirst ? arity - i - 1 : i;

                var input = (OpSubset)rel.Inputs[childId];
                var traits = new List<OpTraitSet>();
                inputTraits.Add(traits);

                int numSubset = input.Set.Subsets.Count;
                for (int j = 0; j < numSubset; j++)
                {
                    var subset = input.Set.Subsets[j];
                    if (!subset.IsDelivered || subset.Traits.EqualsSansConvention(rel.Cluster.TraitSet))
                        continue;

                    if (mode == DeriveMode.Omakase)
                    {
                        traits.Add(subset.Traits);
                    }
                    else
                    {
                        var newRel = rel.Derive(subset.Traits, childId);
                        if (newRel is not null && !_driver._planner.IsRegistered(newRel))
                        {
                            var newInput = newRel.Inputs[childId];
                            if (ReferenceEquals(newInput, subset))
                                subset.DisableEnforcing();

                            _driver._planner.Register(newRel, rel);
                        }
                    }
                }

                if (mode == DeriveMode.LeftFirst || mode == DeriveMode.RightFirst)
                    break;
            }

            if (mode == DeriveMode.Omakase)
            {
                foreach (var newRel in rel.Derive(inputTraits))
                    if (!_driver._planner.IsRegistered(newRel))
                        _driver._planner.Register(newRel, rel);
            }
        }
    }

}
