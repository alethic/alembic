using System.Collections.Generic;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan;

/// <summary>
/// A planner: trait dimensions and rules are registered with it, then it takes a root node, applies
/// the rules, and returns the best equivalent plan it found.
/// </summary>
public interface IPlanner
{

    /// <summary>
    /// Registers a trait dimension. Must be done before <see cref="EmptyTraitSet"/> is used.
    /// </summary>
    void AddTraitDef(ITraitDef def);

    /// <summary>
    /// The registered trait dimensions.
    /// </summary>
    IReadOnlyList<ITraitDef> TraitDefs { get; }

    /// <summary>
    /// The trait set in which every registered dimension carries its default.
    /// </summary>
    TraitSet EmptyTraitSet { get; }

    /// <summary>
    /// The factory that creates this planner's costs.
    /// </summary>
    ICostFactory CostFactory { get; }

    /// <summary>
    /// Registers a rule with the planner.
    /// </summary>
    void AddRule(IRule rule);

    /// <summary>
    /// Registers a listener for planning events.
    /// </summary>
    void AddListener(IPlannerListener listener);

    /// <summary>
    /// Sets the root of the plan to optimize.
    /// </summary>
    void SetRoot(INode node);

    /// <summary>
    /// Records that the plan rooted at <paramref name="node"/> must end up with the given traits, and
    /// returns an equivalent node carrying that requirement. The planner enforces it during
    /// <see cref="FindBestPlan"/>; if it cannot be met, that call throws <see cref="CannotPlanException"/>.
    /// </summary>
    INode ChangeTraits(INode node, TraitSet toTraits);

    /// <summary>
    /// Runs the planner and returns the resulting plan.
    /// </summary>
    INode FindBestPlan();

}
