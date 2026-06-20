using Alembic.Algebra;

namespace Alembic.Plan;

/// <summary>
/// A planner: takes a root node, applies rules, and returns the best equivalent plan it found.
/// </summary>
public interface IPlanner
{

    /// <summary>
    /// Sets the root of the plan to optimize.
    /// </summary>
    void SetRoot(INode node);

    /// <summary>
    /// Runs the planner and returns the resulting plan.
    /// </summary>
    INode FindBestPlan();

}
