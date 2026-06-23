using System;
using System.Collections.Generic;

using Alembic.Algebra;
using Alembic.Plan.Rules;
using Alembic.Util;

namespace Alembic.Plan;

/// <summary>
/// A trait dimension — the set of mutually exclusive values a node may carry along one axis
/// (e.g. convention). Defs are singletons and are registered with the planner
/// (<see cref="IPlanner.AddTraitDef"/>), which builds the empty trait set from them. The strongly-typed
/// <see cref="TraitDef{TTrait}"/> subclass yields its trait type from a <see cref="TraitSet"/>; this
/// non-generic base is the handle used wherever the trait type is not statically known.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef")]
public abstract class TraitDef
{

    readonly WeakInterner<ITrait> _interned = new WeakInterner<ITrait>(EqualityComparer<ITrait>.Default);

    /// <summary>
    /// A stable name for this dimension.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "getSimpleName()")]
    public abstract string Name { get; }

    /// <summary>
    /// The type of the traits on this dimension.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "getTraitClass()")]
    public abstract Type TraitClass { get; }

    /// <summary>
    /// The value a node carries on this dimension when none is specified.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "getDefault()")]
    public abstract ITrait Default { get; }

    /// <summary>
    /// Whether a node may carry several values on this dimension at once (folded into a composite). The
    /// default is no.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "multiple()")]
    public virtual bool Multiple => false;

    /// <summary>
    /// Returns the canonical (interned) instance equal to <paramref name="trait"/>, so equal traits share
    /// one object and can be compared by reference.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "canonize(RelTrait)")]
    public ITrait Canonize(ITrait trait)
    {
        return _interned.Intern(trait);
    }

    /// <summary>
    /// Whether this dimension can convert <paramref name="fromTrait"/> to <paramref name="toTrait"/>
    /// itself (an alternative to a registered converter rule). The default is no.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "canConvert(RelOptPlanner, RelTrait, RelTrait)")]
    public virtual bool CanConvert(IPlanner planner, ITrait fromTrait, ITrait toTrait)
    {
        return false;
    }

    /// <summary>
    /// Converts <paramref name="node"/> to <paramref name="toTrait"/> on this dimension, returning the
    /// converted node (e.g. wrapping it in an enforcer) or <c>null</c> to decline. Consulted only when
    /// <see cref="CanConvert"/> allows it. <paramref name="allowInfiniteCostConverters"/> permits a
    /// converter even when it would carry an infinite cost.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "convert(RelOptPlanner, RelNode, RelTrait, boolean)")]
    public virtual INode? Convert(IPlanner planner, INode node, ITrait toTrait, bool allowInfiniteCostConverters)
    {
        return null;
    }

    /// <summary>
    /// Registers a converter rule that operates on this dimension. The default does nothing.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "registerConverterRule(RelOptPlanner, ConverterRule)")]
    public virtual void RegisterConverterRule(IPlanner planner, ConverterRule converterRule)
    {
    }

    /// <summary>
    /// Removes a previously registered converter rule. The default does nothing.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "deregisterConverterRule(RelOptPlanner, ConverterRule)")]
    public virtual void DeregisterConverterRule(IPlanner planner, ConverterRule converterRule)
    {
    }

}

/// <summary>
/// Strongly-typed base for a trait dimension. A def of <typeparamref name="TTrait"/> yields
/// <typeparamref name="TTrait"/> values from a <see cref="TraitSet"/>.
/// </summary>
/// <typeparam name="TTrait">The trait type carried on this dimension.</typeparam>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef")]
public abstract class TraitDef<TTrait> : TraitDef
    where TTrait : class, ITrait
{

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "getTraitClass()")]
    public override Type TraitClass => typeof(TTrait);

    /// <summary>
    /// The value a node carries on this dimension when none is specified.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "getDefault()")]
    public abstract override TTrait Default { get; }

}
