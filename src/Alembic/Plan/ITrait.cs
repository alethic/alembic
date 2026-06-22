namespace Alembic.Plan;

/// <summary>
/// A single physical property of a node (a convention, an ordering, etc.). Traits are values:
/// equal traits must be <c>Equals</c>-equal.
/// </summary>
public interface ITrait
{

    /// <summary>
    /// The dimension this trait belongs to.
    /// </summary>
    ITraitDef Def { get; }

    /// <summary>
    /// Whether a node carrying this trait also satisfies a requirement for <paramref name="other"/>.
    /// Defaults to equality; traits with a partial order (orderings, distributions) override it.
    /// </summary>
    bool Satisfies(ITrait other)
    {
        return this.Equals(other);
    }

    /// <summary>
    /// Registers this trait instance with the planner — an opportunity to add the rules that relate
    /// to it. Typical implementations do nothing.
    /// </summary>
    void Register(IPlanner planner)
    {

    }

}
