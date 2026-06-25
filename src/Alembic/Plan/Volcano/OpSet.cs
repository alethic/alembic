using System.Collections.Generic;
using System.Diagnostics;

using Alembic.Algebra;
using Alembic.Algebra.Convert;
using Alembic.Util;

namespace Alembic.Plan.Volcano;

/// <summary>
/// An equivalence set: a group of ops that all produce the same result, partitioned into
/// <see cref="OpSubset"/>s by trait set. The planner keeps the cheapest member of each subset.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet")]
internal class OpSet
{

    // Trait-set pairs already wired with a converter, so each conversion is seeded at most once.
    readonly HashSet<Pair<OpTraitSet, OpTraitSet>> _conversions = new HashSet<Pair<OpTraitSet, OpTraitSet>>();

    // NOTE: Calcite's RelSet(int, Set<CorrelationId>, Set<CorrelationId>) also records the correlation
    // variables propagated/used by the set; those parameters are omitted here until correlation (Rex) is
    // ported.
    /// <summary>
    /// Creates an empty set with the given <paramref name="id"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "RelSet(int, Set<CorrelationId>, Set<CorrelationId>)")]
    internal OpSet(int id)
    {
        Id = id;
    }

    /// <summary>
    /// This set's stable identity.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "id")]
    internal readonly int Id;

    /// <summary>
    /// Every op in the set (across all subsets).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "rels")]
    internal readonly List<IOp> Ops = new List<IOp>();

    /// <summary>
    /// The subsets of this set, one per distinct trait set.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "subsets")]
    internal readonly List<OpSubset> Subsets = new List<OpSubset>();

    /// <summary>
    /// Ops (in other sets) that reference a subset of this set as a child.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "parents")]
    internal readonly List<IOp> Parents = new List<IOp>();

    /// <summary>
    /// Set when this set is merged into another; the live set is reached by following the chain.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "equivalentSet")]
    internal OpSet? EquivalentSet;

    /// <summary>
    /// How far the top-down search has explored this set (applied transformation rules to its members).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet.ExploringState")]
    public enum ExploringState
    {

        /// <summary>
        /// Exploration is underway.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet.ExploringState", "EXPLORING")]
        Exploring,

        /// <summary>
        /// The set is fully explored.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet.ExploringState", "EXPLORED")]
        Explored

    }

    /// <summary>
    /// This set's exploration state, or <c>null</c> if exploration has not started.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "exploringState")]
    internal ExploringState? Exploring;

    /// <summary>
    /// The first op registered in this set — its representative expression, used as a fallback when a
    /// subset has no best member yet.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "rel")]
    internal IOp? Rel;

    /// <summary>
    /// The subset with exactly the given traits, or <c>null</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "getSubset(RelTraitSet)")]
    public OpSubset? GetSubset(OpTraitSet traits)
    {
        foreach (var subset in Subsets)
            if (subset.Traits.Equals(traits))
                return subset;

        return null;
    }

    /// <summary>
    /// Notifies the planner's listener that <paramref name="op"/> has joined this set's equivalence class.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "postEquivalenceEvent(VolcanoPlanner, RelNode)")]
    void PostEquivalenceEvent(VolcanoPlanner planner, IOp op)
    {
        var listener = planner.Listener;
        if (listener is not null)
        {
            var @event = new IPlannerListener.OpEquivalenceEvent(planner, op, "equivalence class " + Id, false);
            listener.OpEquivalenceFound(@event);
        }
    }

    /// <summary>
    /// Gets or creates the subset for the given traits, marking it required (a parent asks for it) or
    /// delivered (a member produces it). A logical (<see cref="Convention.None"/>) subset is neither.
    /// When a subset is freshly created — or first becomes required/delivered — converters/enforcers are
    /// seeded between it and the complementary subsets so the planner can convert between them.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "getOrCreateSubset(RelOptCluster, RelTraitSet, boolean)")]
    internal OpSubset GetOrCreateSubset(OpCluster cluster, OpTraitSet traits, bool required)
    {
        var planner = (VolcanoPlanner)cluster.Planner;
        var needsConverter = false;
        var subset = GetSubset(traits);
        if (subset is null)
        {
            needsConverter = true;
            subset = new OpSubset(cluster, this, traits);

            // Need to first add to the set before adding the abstract converters (for others->subset),
            // since otherwise during register() the planner will try to add this subset again.
            Subsets.Add(subset);

            if (planner.Listener is not null)
                PostEquivalenceEvent(planner, subset);
        }
        else if ((required && !subset.IsRequired) || (!required && !subset.IsDelivered))
        {
            needsConverter = true;
        }

        if (subset.Traits.Convention.Equals(Convention.None))
            needsConverter = false;
        else if (required)
            subset.SetRequired();
        else
            subset.SetDelivered();

        if (needsConverter)
            AddConverters(subset, required, useAbstractConverter: !planner.TopDownOpt);

        return subset;
    }

    /// <summary>
    /// Seeds converters (or convention enforcers) between <paramref name="subset"/> and the complementary
    /// subsets of this set: when <paramref name="subset"/> is required, from each delivered subset to it;
    /// when delivered, from it to each required subset. A conversion is seeded only once per trait-set
    /// pair, and only when the dimensions actually differ and the trait dimension can convert.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "addConverters(RelSubset, boolean, boolean)")]
    void AddConverters(OpSubset subset, bool required, bool useAbstractConverter)
    {
        var cluster = subset.Cluster;
        var planner = (VolcanoPlanner)cluster.Planner;

        var others = new List<OpSubset>();
        foreach (var n in Subsets)
            if (required ? n.IsDelivered : n.IsRequired)
                others.Add(n);

        foreach (var other in others)
        {
            var from = subset;
            var to = other;
            if (required)
            {
                from = other;
                to = subset;
            }

            if (from == to
                || to.IsEnforceDisabled
                || (useAbstractConverter
                    && from.Traits.Convention is not null
                    && !from.Traits.Convention.UseAbstractConvertersForConversion(from.Traits, to.Traits)))
            {
                continue;
            }

            if (!_conversions.Add(Pair.Of(from.Traits, to.Traits)))
                continue;

            var needsConverter = false;
            foreach (var fromTrait in to.Traits.Difference(from.Traits))
            {
                var traitDef = fromTrait.TraitDef;
                IOpTrait? toTrait = to.Traits.Get(traitDef);

                if (toTrait is null || !traitDef.CanConvert(planner, fromTrait, toTrait))
                {
                    needsConverter = false;
                    break;
                }

                if (!fromTrait.Satisfies(toTrait))
                    needsConverter = true;
            }

            if (needsConverter)
            {
                var enforcer = useAbstractConverter
                    ? new AbstractConverter(from.Cluster, from, null, to.Traits)
                    : subset.Traits.Convention.Enforce(from, to.Traits);

                if (enforcer is not null)
                    planner.Register(enforcer, to);
            }
        }
    }

    /// <summary>
    /// Adds <paramref name="op"/> to the set, placing it in the subset for its traits (creating that
    /// subset if needed) and returning the subset.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "add(RelNode)")]
    internal OpSubset Add(IOp op)
    {
        Debug.Assert(EquivalentSet is null, "adding to a dead set");
        var traits = op.Traits.Simplify();
        var subset = GetOrCreateSubset(op.Cluster, traits, op.IsEnforcer);
        subset.Add(op);
        return subset;
    }

    /// <summary>
    /// Appends <paramref name="op"/> to the set's op list (if not already present), firing the
    /// equivalence-found notification, and records it as the set's representative when the set has none yet.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "addInternal(RelNode, VolcanoPlanner)")]
    internal void AddInternal(IOp op)
    {
        if (!Ops.Contains(op))
        {
            Ops.Add(op);
            PostEquivalenceEvent((VolcanoPlanner)op.Cluster.Planner, op);
        }

        if (Rel is null)
            Rel = op;
        else
            // Output types must be the same, except for cosmetic naming.
            PlanUtil.VerifyTypeEquivalence(Rel, op, this);
    }

    /// <summary>
    /// Drops <paramref name="op"/> from this set's parent list (called when the op is being removed
    /// from the planner).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "obliterateRelNode(RelNode)")]
    internal void ObliterateOp(IOp op) => Parents.Remove(op);

    /// <summary>
    /// The live sets this set's (non-converter) members consume as inputs — its child sets in the
    /// equivalence-set graph. Used by the merge to decide the swap direction.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "getChildSets(VolcanoPlanner)")]
    internal HashSet<OpSet> GetChildSets()
    {
        var childSets = new HashSet<OpSet>();
        foreach (var op in Ops)
        {
            if (op is IConverter)
                continue;

            foreach (var child in op.Inputs)
            {
                var childSet = VolcanoPlanner.EquivRoot(((OpSubset)child).Set);
                if (!ReferenceEquals(childSet, this))
                    childSets.Add(childSet);
            }
        }

        return childSets;
    }

    /// <summary>
    /// Absorbs <paramref name="other"/> into this set: <paramref name="other"/> becomes equivalent to
    /// this set, its ops and parents move here, and the parents are re-registered to point at the
    /// surviving set. The caller (<see cref="VolcanoPlanner.Merge"/>) has already resolved both sets to
    /// their equivalence roots.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.RelSet", "mergeWith(VolcanoPlanner, RelSet)")]
    internal void MergeWith(VolcanoPlanner planner, OpSet other)
    {
        Debug.Assert(!ReferenceEquals(this, other));
        Debug.Assert(EquivalentSet is null);
        Debug.Assert(other.EquivalentSet is null);
        other.EquivalentSet = this;

        // remove from table
        var existed = planner.RemoveSet(other);
        Debug.Assert(existed, "merging with a dead otherSet");

        var cluster = Rel!.Cluster;
        var changedRels = new HashSet<IOp>(ReferenceEqualityComparer.Instance);

        // Merge subsets: recreate each of the other set's subsets here (with the same required/delivered
        // flags), fold in its pass-through cache, and note any whose best cost should now improve.
        foreach (var otherSubset in other.Subsets)
        {
            OpSubset? subset = null;
            var otherTraits = otherSubset.Traits;

            // A logical or delivered-physical subset.
            if (otherSubset.IsDelivered || !otherSubset.IsRequired)
                subset = GetOrCreateSubset(cluster, otherTraits, false);

            // It may be required only, or both delivered and required, in which case register again.
            if (otherSubset.IsRequired)
                subset = GetOrCreateSubset(cluster, otherTraits, true);

            subset!.AdoptPassThroughCache(otherSubset);

            if (otherSubset.BestCost.IsLessThan(subset.BestCost) && otherSubset.Best is not null)
                changedRels.Add(otherSubset.Best);
        }

        var parentRels = new HashSet<IOp>(Parents, ReferenceEqualityComparer.Instance);
        foreach (var otherRel in other.Ops)
        {
            // If otherRel is an enforcing operator (e.g. a converter) do not prune it; otherwise, when it
            // is a parent that adds nothing over its single input, it is redundant and can be pruned.
            if (!otherRel.IsEnforcer && parentRels.Contains(otherRel))
            {
                if (otherRel.Inputs.Length != 1
                    || otherRel.Inputs[0].Traits.Satisfies(otherRel.Traits))
                {
                    planner.Prune(otherRel);
                }
            }

            planner.ReRegister(this, otherRel);
        }

        // Has another set merged with this?
        Debug.Assert(EquivalentSet is null);

        // Propagate the new best information from the changed ops.
        foreach (var rel in changedRels)
            planner.PropagateCostImprovements(rel);

        // Update every op that had a child in the other set, to reflect the renamed child. Copy first,
        // since renaming mutates the parent lists.
        var previousParents = new List<IOp>(other.Parents);
        foreach (var parentRel in previousParents)
            planner.Rename(parentRel);

        // Renaming may have caused this set to merge into another; if so this set is now obsolete and its
        // children must not be touched.
        if (EquivalentSet is not null)
            return;

        // Make sure cost changes from the merge are propagated to the parents.
        foreach (var parentRel in Parents)
            planner.PropagateCostImprovements(parentRel);
        Debug.Assert(EquivalentSet is null);

        // Each op in the old set now has new parents, so rules may fire again, as if newly registered.
        foreach (var op in Ops)
        {
            Debug.Assert(ReferenceEquals(planner.GetSet(op), this));
            planner.FireRules(op);
        }

        // Fire rule matches on the subsets as well.
        foreach (var subset in Subsets)
            planner.FireRules(subset);
    }

}
