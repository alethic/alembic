namespace Alembic.Plan;

/// <summary>
/// Creates the well-known costs a planner needs — zero (a free leaf), infinite (an unimplementable or
/// rejected plan), and the huge/tiny bookends. A planner carries one and the cost-bearing nodes use it
/// to build their own costs.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostFactory")]
public interface ICostFactory
{

    /// <summary>
    /// A cost from a CPU and an I/O estimate. A scalar cost model may use only some of them (the
    /// default <see cref="Cost"/> keeps the CPU estimate).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostFactory", "makeCost(double, double, double)")]
    ICost MakeCost(double cpu, double io);

    /// <summary>
    /// A cost of zero.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostFactory", "makeZeroCost()")]
    ICost MakeZeroCost();

    /// <summary>
    /// An infinite cost — an unimplementable or rejected plan.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostFactory", "makeInfiniteCost()")]
    ICost MakeInfiniteCost();

    /// <summary>
    /// An enormous but finite cost.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostFactory", "makeHugeCost()")]
    ICost MakeHugeCost();

    /// <summary>
    /// A small positive cost.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostFactory", "makeTinyCost()")]
    ICost MakeTinyCost();

}
