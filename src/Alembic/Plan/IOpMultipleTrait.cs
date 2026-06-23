using System;

namespace Alembic.Plan;

/// <summary>
/// A trait for which an op can have several values at once — the classic example is sortedness, where
/// a table may be sorted by <c>[year, month, day]</c> and also by <c>[id]</c>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelMultipleTrait")]
public interface IOpMultipleTrait : IOpTrait, IComparable<IOpMultipleTrait>
{

    /// <summary>
    /// Whether this trait is satisfied by every instance of the trait (including itself) — the top of
    /// the order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelMultipleTrait", "isTop()")]
    bool IsTop { get; }

}
