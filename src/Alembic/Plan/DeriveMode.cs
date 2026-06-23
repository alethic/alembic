namespace Alembic.Plan;

/// <summary>
/// How a physical op derives traits from its already-optimized inputs during the top-down search.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.DeriveMode")]
public enum DeriveMode
{

    /// <summary>
    /// Tries deriving from the first input only.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.DeriveMode", "LEFT_FIRST")]
    LeftFirst,

    /// <summary>
    /// Tries deriving from the last input only.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.DeriveMode", "RIGHT_FIRST")]
    RightFirst,

    /// <summary>
    /// Tries deriving from every input.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.DeriveMode", "BOTH")]
    Both,

    /// <summary>
    /// Hands the op the full matrix of input trait sets and lets it decide what to produce.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.DeriveMode", "OMAKASE")]
    Omakase,

    /// <summary>
    /// Derives nothing.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.DeriveMode", "PROHIBITED")]
    Prohibited

}
