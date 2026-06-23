using System.Collections.Generic;
using System.Text.RegularExpressions;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan;

/// <summary>
/// A planner: trait dimensions and rules are registered with it, then it takes a root node, applies
/// the rules, and returns the best equivalent plan it found.
/// </summary>
[Provenance("org.apache.calcite.plan.RelOptPlanner")]
public interface IPlanner
{

    /// <summary>
    /// Registers a trait dimension. Must be done before <see cref="EmptyTraitSet"/> is used.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "addRelTraitDef(RelTraitDef)")]
    void AddTraitDef(TraitDef def);

    /// <summary>
    /// The registered trait dimensions.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "getRelTraitDefs()")]
    IReadOnlyList<TraitDef> TraitDefs { get; }

    /// <summary>
    /// The trait set in which every registered dimension carries its default.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "emptyTraitSet()")]
    TraitSet EmptyTraitSet { get; }

    /// <summary>
    /// The factory that creates this planner's costs.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "getCostFactory()")]
    ICostFactory CostFactory { get; }

    /// <summary>
    /// Registers a rule with the planner. Returns whether the rule was added — <c>false</c> if a rule
    /// with the same description was already registered (a duplicate registration is a no-op).
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "addRule(RelOptRule)")]
    bool AddRule(Rule rule);

    /// <summary>
    /// Removes a previously registered rule; returns whether it was registered.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "removeRule(RelOptRule)")]
    bool RemoveRule(Rule rule);

    /// <summary>
    /// Prunes a node so that the planner no longer expands it. The default planner ignores this.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "prune(RelNode)")]
    void Prune(INode node);

    /// <summary>
    /// Resets the planner's registered state (rules and any search state) so it can be reused. The base
    /// planner does nothing; concrete planners override.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "clear()")]
    void Clear();

    /// <summary>
    /// Removes all registered trait dimensions.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "clearRelTraitDefs()")]
    void ClearTraitDefs();

    /// <summary>
    /// Sets a filter that excludes rules whose description matches it from firing, or clears the filter
    /// when <c>null</c>.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "setRuleDescExclusionFilter(Pattern)")]
    void SetRuleDescExclusionFilter(Regex? exclusionFilter);

    /// <summary>
    /// Whether <paramref name="rule"/> is excluded from firing by the description exclusion filter.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "isRuleExcluded(RelOptRule)")]
    bool IsRuleExcluded(Rule rule);

    /// <summary>
    /// Registers a listener for planning events.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "addListener(RelOptListener)")]
    void AddListener(IPlannerListener listener);

    /// <summary>
    /// Sets the root of the plan to optimize.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "setRoot(RelNode)")]
    void SetRoot(INode node);

    /// <summary>
    /// The current root of the plan, or <c>null</c> if none is set.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "getRoot()")]
    INode? Root { get; }

    /// <summary>
    /// Records that the plan rooted at <paramref name="node"/> must end up with the given traits, and
    /// returns an equivalent node carrying that requirement. The planner enforces it during
    /// <see cref="FindBestPlan"/>; if it cannot be met, that call throws <see cref="CannotPlanException"/>.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "changeTraits(RelNode, RelTraitSet)")]
    INode ChangeTraits(INode node, TraitSet toTraits);

    /// <summary>
    /// Returns <paramref name="node"/> converted to <paramref name="toTraits"/> — for the cost-based
    /// planner, the equivalence subset carrying those traits — <em>without</em> making it the root.
    /// This is the conversion primitive (<see cref="ChangeTraits"/> is this plus a root request); the
    /// top-down search uses it to convert a physical node's inputs.
    /// </summary>
    INode Convert(INode node, TraitSet toTraits);

    /// <summary>
    /// Runs the planner and returns the resulting plan.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner", "findBestExp()")]
    INode FindBestPlan();

}
