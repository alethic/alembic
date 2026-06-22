namespace Alembic.Plan;

/// <summary>
/// A comparable, combinable plan cost. The engine treats cost opaquely: it only compares costs and
/// adds them up, so a consumer is free to model cost however they like (a single magnitude, a tuple of
/// dimensions, etc.).
/// </summary>
public interface ICost
{

    /// <summary>
    /// Whether this cost is infinite (an unimplementable or rejected plan).
    /// </summary>
    bool IsInfinite { get; }

    /// <summary>
    /// Whether this cost is less than or equal to <paramref name="other"/>.
    /// </summary>
    bool IsLessThanOrEqual(ICost other);

    /// <summary>
    /// Whether this cost is strictly less than <paramref name="other"/>.
    /// </summary>
    bool IsLessThan(ICost other);

    /// <summary>
    /// This cost combined with <paramref name="other"/> (e.g. a node's self-cost plus its inputs').
    /// </summary>
    ICost Plus(ICost other);

}
