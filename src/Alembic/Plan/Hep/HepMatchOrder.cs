namespace Alembic.Plan.Hep;

/// <summary>
/// The order in which a <see cref="HepPlanner"/> visits nodes when applying rules.
/// </summary>
public enum HepMatchOrder
{

    /// <summary>
    /// No particular order (treated as <see cref="BottomUp"/>).
    /// </summary>
    Arbitrary,

    /// <summary>
    /// Children before parents — the usual order for lowering.
    /// </summary>
    BottomUp,

    /// <summary>
    /// Parents before children.
    /// </summary>
    TopDown,

    /// <summary>
    /// Depth-first (treated as <see cref="BottomUp"/> for now).
    /// </summary>
    DepthFirst,

}
