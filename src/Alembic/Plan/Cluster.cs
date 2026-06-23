namespace Alembic.Plan;

/// <summary>
/// The environment for a planning session: the planner, minus relational-algebra-specific pieces
/// (row-expression builder, metadata query). The trait registry and empty trait set live on the
/// planner.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster")]
public sealed class Cluster
{

    /// <summary>
    /// Creates a cluster over the given planner.
    /// </summary>
    public Cluster(IPlanner planner)
    {
        Planner = planner;
    }

    /// <summary>
    /// The planner for this session.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster", "getPlanner()")]
    public IPlanner Planner { get; }

    /// <summary>
    /// The default trait set for this cluster. By default the planner's empty trait set (every
    /// registered dimension at its default); the name is generic — not "empty" — because a cluster may
    /// override it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster", "traitSet()")]
    public TraitSet TraitSet => Planner.EmptyTraitSet;

    /// <summary>
    /// The default trait set with the given traits applied.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster", "traitSetOf(RelTrait...)")]
    public TraitSet TraitSetOf(params ITrait[] traits)
    {
        var result = TraitSet;
        foreach (var trait in traits)
            result = result.Plus(trait);

        return result;
    }

}
