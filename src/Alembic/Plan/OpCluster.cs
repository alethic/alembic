using System;

using Alembic.Algebra.Metadata;

namespace Alembic.Plan;

/// <summary>
/// The environment for a planning session: the planner and the metadata query, minus the relational
/// row-expression builder. The trait registry and empty trait set live on the planner.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster")]
public class OpCluster
{

    OpMetadataQuery? _mq;
    Func<OpMetadataQuery> _mqSupplier;

    /// <summary>
    /// Creates a cluster over the given planner.
    /// </summary>
    public OpCluster(IOpPlanner planner)
    {
        Planner = planner;
        _mqSupplier = () => new OpMetadataQuery();
    }

    /// <summary>
    /// The planner for this session.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster", "getPlanner()")]
    public IOpPlanner Planner { get; }

    /// <summary>
    /// Sets the supplier of <see cref="OpMetadataQuery"/> instances. The supplier must return a fresh
    /// instance, since the cluster caches it and invalidates it across rule-call cycles.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster", "setMetadataQuerySupplier(Supplier)")]
    public void SetMetadataQuerySupplier(Func<OpMetadataQuery> mqSupplier) => _mqSupplier = mqSupplier;

    /// <summary>
    /// The supplier of <see cref="OpMetadataQuery"/> instances.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster", "getMetadataQuerySupplier()")]
    public Func<OpMetadataQuery> GetMetadataQuerySupplier() => _mqSupplier;

    /// <summary>
    /// The current metadata query, created on demand from the supplier and cached until invalidated.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster", "getMetadataQuery()")]
    public OpMetadataQuery GetMetadataQuery() => _mq ??= _mqSupplier();

    /// <summary>
    /// Invalidates the current metadata query; the next <see cref="GetMetadataQuery"/> builds a fresh
    /// one. Called whenever the plan changes (typically from a rule call's transformTo).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster", "invalidateMetadataQuery()")]
    public void InvalidateMetadataQuery() => _mq = null;

    /// <summary>
    /// The default trait set for this cluster. By default the planner's empty trait set (every
    /// registered dimension at its default); the name is generic — not "empty" — because a cluster may
    /// override it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster", "traitSet()")]
    public OpTraitSet TraitSet => Planner.EmptyTraitSet;

    /// <summary>
    /// The default trait set with the given traits applied.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster", "traitSetOf(RelTrait...)")]
    public OpTraitSet TraitSetOf(params IOpTrait[] traits)
    {
        var result = TraitSet;
        foreach (var trait in traits)
            result = result.Replace(trait);

        return result;
    }

    /// <summary>
    /// The default trait set with the given trait applied.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCluster", "traitSetOf(RelTrait)")]
    public OpTraitSet TraitSetOf(IOpTrait trait) => TraitSet.Replace(trait);

}
