using System.Collections.Generic;
using System.Text.RegularExpressions;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan;

/// <summary>
/// A planner: trait dimensions and rules are registered with it, then it takes a root op, applies
/// the rules, and returns the best equivalent plan it found.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner")]
public interface IOpPlanner
{

    /// <summary>
    /// Registers a trait dimension. Must be done before <see cref="EmptyTraitSet"/> is used.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "addRelTraitDef(RelTraitDef)")]
    void AddTraitDef(OpTraitDef def);

    /// <summary>
    /// The registered trait dimensions.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "getRelTraitDefs()")]
    IReadOnlyList<OpTraitDef> TraitDefs { get; }

    /// <summary>
    /// The trait set in which every registered dimension carries its default.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "emptyTraitSet()")]
    OpTraitSet EmptyTraitSet { get; }

    /// <summary>
    /// The factory that creates this planner's costs.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "getCostFactory()")]
    IOpCostFactory CostFactory { get; }

    /// <summary>
    /// Registers a rule with the planner. Returns whether the rule was added — <c>false</c> if a rule
    /// with the same description was already registered (a duplicate registration is a no-op).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "addRule(RelOptRule)")]
    bool AddRule(OpRule rule);

    /// <summary>
    /// Removes a previously registered rule; returns whether it was registered.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "removeRule(RelOptRule)")]
    bool RemoveRule(OpRule rule);

    /// <summary>
    /// Prunes an op so that the planner no longer expands it. The default planner ignores this.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "prune(RelNode)")]
    void Prune(IOp op);

    /// <summary>
    /// Resets the planner's registered state (rules and any search state) so it can be reused. The base
    /// planner does nothing; concrete planners override.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "clear()")]
    void Clear();

    /// <summary>
    /// Removes all registered trait dimensions.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "clearRelTraitDefs()")]
    void ClearTraitDefs();

    /// <summary>
    /// Sets a filter that excludes rules whose description matches it from firing, or clears the filter
    /// when <c>null</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "setRuleDescExclusionFilter(Pattern)")]
    void SetRuleDescExclusionFilter(Regex? exclusionFilter);

    /// <summary>
    /// Whether <paramref name="rule"/> is excluded from firing by the description exclusion filter.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "isRuleExcluded(RelOptRule)")]
    bool IsRuleExcluded(OpRule rule);

    /// <summary>
    /// Registers a listener for planning events.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "addListener(RelOptListener)")]
    void AddListener(IPlannerListener listener);

    /// <summary>
    /// Sets the root of the plan to optimize.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "setRoot(RelNode)")]
    void SetRoot(IOp op);

    /// <summary>
    /// The current root of the plan, or <c>null</c> if none is set.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "getRoot()")]
    IOp? Root { get; }

    /// <summary>
    /// Returns an equivalent of <paramref name="op"/> with the given traits, registering it if needed —
    /// for the cost-based planner, the equivalence subset carrying those traits. This does not change the
    /// planner's root; compose it with <see cref="SetRoot"/> to optimize toward those traits.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "changeTraits(RelNode, RelTraitSet)")]
    IOp ChangeTraits(IOp op, OpTraitSet toTraits);

    /// <summary>
    /// Registers <paramref name="op"/> if it is not already registered, returning its registered form (for
    /// the cost-based planner, the equivalence subset). <paramref name="equivalent"/>, when non-null, is an
    /// already-registered op known to be equivalent. Called by <see cref="IOp.OnRegister"/> for each input.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "ensureRegistered(RelNode, RelNode)")]
    IOp EnsureRegistered(IOp op, IOp? equivalent);

    /// <summary>
    /// Runs the planner and returns the resulting plan.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptPlanner", "findBestExp()")]
    IOp FindBestPlan();

}
