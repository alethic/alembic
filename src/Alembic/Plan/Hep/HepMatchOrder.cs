namespace Alembic.Plan.Hep;

/// <summary>
/// The order in which a <see cref="HepPlanner"/> visits nodes when applying rules.
/// </summary>
[Provenance("org.apache.calcite.plan.hep.HepMatchOrder")]
public enum HepMatchOrder
{

    /// <summary>
    /// No particular order (treated as <see cref="BottomUp"/>).
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepMatchOrder.ARBITRARY")]
    Arbitrary,

    /// <summary>
    /// Children before parents — the usual order for lowering.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepMatchOrder.BOTTOM_UP")]
    BottomUp,

    /// <summary>
    /// Parents before children.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepMatchOrder.TOP_DOWN")]
    TopDown,

    /// <summary>
    /// Depth-first (treated as <see cref="BottomUp"/> for now).
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepMatchOrder.DEPTH_FIRST")]
    DepthFirst,

}
