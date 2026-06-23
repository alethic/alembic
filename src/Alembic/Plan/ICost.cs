namespace Alembic.Plan;

/// <summary>
/// A comparable, combinable plan cost. The engine treats cost opaquely: it only compares costs and
/// adds them up, so a consumer is free to model cost however they like (a single magnitude, a tuple of
/// dimensions, etc.).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCost")]
public interface ICost
{

    /// <summary>
    /// The CPU component of this cost.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCost", "getCpu()")]
    double Cpu { get; }

    /// <summary>
    /// The I/O component of this cost.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCost", "getIo()")]
    double Io { get; }

    /// <summary>
    /// Whether this cost is infinite (an unimplementable or rejected plan).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCost", "isInfinite()")]
    bool IsInfinite { get; }

    /// <summary>
    /// Whether this cost is less than or equal to <paramref name="other"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCost", "isLe(RelOptCost)")]
    bool IsLessThanOrEqual(ICost other);

    /// <summary>
    /// Whether this cost is strictly less than <paramref name="other"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCost", "isLt(RelOptCost)")]
    bool IsLessThan(ICost other);

    /// <summary>
    /// This cost combined with <paramref name="other"/> (e.g. an op's self-cost plus its inputs').
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCost", "plus(RelOptCost)")]
    ICost Plus(ICost other);

    /// <summary>
    /// This cost with <paramref name="other"/> removed (the inverse of <see cref="Plus"/>); used by the
    /// top-down search to apportion an upper bound across an op and its inputs.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCost", "minus(RelOptCost)")]
    ICost Minus(ICost other);

    /// <summary>
    /// This cost scaled by <paramref name="factor"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCost", "multiplyBy(double)")]
    ICost MultiplyBy(double factor);

    /// <summary>
    /// This cost divided by <paramref name="other"/>, as a single ratio: the geometric mean of the
    /// per-component ratios over the components that are non-zero and finite in both (1.0 if none are).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCost", "divideBy(RelOptCost)")]
    double DivideBy(ICost other);

    /// <summary>
    /// Whether this cost equals <paramref name="other"/> within a small epsilon on every component.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCost", "isEqWithEpsilon(RelOptCost)")]
    bool IsEqWithEpsilon(ICost other);

}
