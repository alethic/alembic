namespace Alembic.Plan.Hep;

/// <summary>
/// The order in which a <see cref="HepPlanner"/> visits ops when applying rules.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepMatchOrder")]
public enum HepMatchOrder
{

    /// <summary>
    /// No particular order (treated as <see cref="BottomUp"/>).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepMatchOrder.ARBITRARY")]
    Arbitrary,

    /// <summary>
    /// Children before parents — the usual order for lowering.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepMatchOrder.BOTTOM_UP")]
    BottomUp,

    /// <summary>
    /// Parents before children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepMatchOrder.TOP_DOWN")]
    TopDown,

    /// <summary>
    /// Depth-first (treated as <see cref="BottomUp"/> for now).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepMatchOrder.DEPTH_FIRST")]
    DepthFirst,

}
