namespace Alembic.Plan;

/// <summary>
/// How a physical node derives traits from its already-optimized inputs during the top-down search.
/// </summary>
[Provenance("org.apache.calcite.plan.DeriveMode")]
public enum DeriveMode
{

    /// <summary>
    /// Tries deriving from the first input only.
    /// </summary>
    [Provenance("org.apache.calcite.plan.DeriveMode", "LEFT_FIRST")]
    LeftFirst,

    /// <summary>
    /// Tries deriving from the last input only.
    /// </summary>
    [Provenance("org.apache.calcite.plan.DeriveMode", "RIGHT_FIRST")]
    RightFirst,

    /// <summary>
    /// Tries deriving from every input.
    /// </summary>
    [Provenance("org.apache.calcite.plan.DeriveMode", "BOTH")]
    Both,

    /// <summary>
    /// Hands the node the full matrix of input trait sets and lets it decide what to produce.
    /// </summary>
    [Provenance("org.apache.calcite.plan.DeriveMode", "OMAKASE")]
    Omakase,

    /// <summary>
    /// Derives nothing.
    /// </summary>
    [Provenance("org.apache.calcite.plan.DeriveMode", "PROHIBITED")]
    Prohibited

}
