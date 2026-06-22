namespace Alembic.Plan;

/// <summary>
/// How a physical node derives traits from its already-optimized inputs during the top-down search.
/// </summary>
public enum DeriveMode
{

    /// <summary>
    /// Tries deriving from the first input only.
    /// </summary>
    LeftFirst,

    /// <summary>
    /// Tries deriving from the last input only.
    /// </summary>
    RightFirst,

    /// <summary>
    /// Tries deriving from every input.
    /// </summary>
    Both,

    /// <summary>
    /// Hands the node the full matrix of input trait sets and lets it decide what to produce.
    /// </summary>
    Omakase,

    /// <summary>
    /// Derives nothing.
    /// </summary>
    Prohibited

}
