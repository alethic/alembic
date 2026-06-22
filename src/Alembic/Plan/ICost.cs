namespace Alembic.Plan;

/// <summary>
/// A comparable, combinable plan cost. The engine treats cost opaquely: it only compares costs and
/// adds them up, so a consumer is free to model cost however they like (a single magnitude, a tuple of
/// dimensions, etc.).
/// </summary>
public interface ICost
{

    /// <summary>
    /// The CPU component of this cost.
    /// </summary>
    double Cpu { get; }

    /// <summary>
    /// The I/O component of this cost.
    /// </summary>
    double Io { get; }

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

    /// <summary>
    /// This cost with <paramref name="other"/> removed (the inverse of <see cref="Plus"/>); used by the
    /// top-down search to apportion an upper bound across a node and its inputs.
    /// </summary>
    ICost Minus(ICost other);

    /// <summary>
    /// This cost scaled by <paramref name="factor"/>.
    /// </summary>
    ICost MultiplyBy(double factor);

    /// <summary>
    /// This cost divided by <paramref name="other"/>, as a single ratio: the geometric mean of the
    /// per-component ratios over the components that are non-zero and finite in both (1.0 if none are).
    /// </summary>
    double DivideBy(ICost other);

    /// <summary>
    /// Whether this cost equals <paramref name="other"/> within a small epsilon on every component.
    /// </summary>
    bool IsEqWithEpsilon(ICost other);

}
