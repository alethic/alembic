using System;

namespace Alembic.Plan;

/// <summary>
/// Thrown by <see cref="IPlanner.FindBestPlan"/> when the planner cannot produce a plan that satisfies
/// the output traits requested through <see cref="IPlanner.ChangeTraits"/> — typically because no rule
/// chain converts some node into the required form.
/// </summary>
[Provenance("org.apache.calcite.plan.RelOptPlanner.CannotPlanException")]
public sealed class CannotPlanException : Exception
{

    /// <summary>
    /// Creates the exception with a message describing why no satisfying plan was found.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptPlanner.CannotPlanException", "CannotPlanException(String)")]
    public CannotPlanException(string message)
        : base(message)
    {

    }

}
