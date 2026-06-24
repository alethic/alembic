using System;
using System.Collections.Generic;

using Alembic.Algebra;
using Alembic.Algebra.Convert;
using Alembic.Util;

namespace Alembic.Plan;

/// <summary>
/// A trait dimension — the set of mutually exclusive values an op may carry along one axis
/// (e.g. convention). Defs are singletons and are registered with the planner
/// (<see cref="IOpPlanner.AddTraitDef"/>), which builds the empty trait set from them. The strongly-typed
/// <see cref="OpTraitDef{TTrait}"/> subclass yields its trait type from a <see cref="OpTraitSet"/>; this
/// non-generic base is the handle used wherever the trait type is not statically known.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef")]
public abstract class OpTraitDef
{

    readonly IInterner<IOpTrait> _interned = Interners.NewWeakInterner<IOpTrait>();

    /// <summary>
    /// A stable name for this dimension.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "getSimpleName()")]
    public abstract string Name { get; }

    /// <summary>
    /// The type of the traits on this dimension.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "getTraitClass()")]
    public abstract Type TraitType { get; }

    /// <summary>
    /// The value an op carries on this dimension when none is specified.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "getDefault()")]
    public abstract IOpTrait Default { get; }

    /// <summary>
    /// Whether an op may carry several values on this dimension at once (folded into a composite). The
    /// default is no.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "multiple()")]
    public virtual bool Multiple => false;

    /// <summary>
    /// Returns the canonical (interned) instance equal to <paramref name="trait"/>, so equal traits share
    /// one object and can be compared by reference.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "canonize(RelTrait)")]
    public IOpTrait Canonize(IOpTrait trait)
    {
        return _interned.Intern(trait);
    }

    /// <summary>
    /// Whether this dimension can convert <paramref name="fromTrait"/> to <paramref name="toTrait"/>
    /// itself (an alternative to a registered converter rule). The default is no.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "canConvert(RelOptPlanner, RelTrait, RelTrait)")]
    public virtual bool CanConvert(IOpPlanner planner, IOpTrait fromTrait, IOpTrait toTrait)
    {
        return false;
    }

    /// <summary>
    /// Converts <paramref name="op"/> to <paramref name="toTrait"/> on this dimension, returning the
    /// converted op (e.g. wrapping it in an enforcer) or <c>null</c> to decline. Consulted only when
    /// <see cref="CanConvert"/> allows it. <paramref name="allowInfiniteCostConverters"/> permits a
    /// converter even when it would carry an infinite cost.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "convert(RelOptPlanner, RelNode, RelTrait, boolean)")]
    public virtual IOp? Convert(IOpPlanner planner, IOp op, IOpTrait toTrait, bool allowInfiniteCostConverters)
    {
        return null;
    }

    /// <summary>
    /// Registers a converter rule that operates on this dimension. The default does nothing.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "registerConverterRule(RelOptPlanner, ConverterRule)")]
    public virtual void RegisterConverterRule(IOpPlanner planner, ConverterRule converterRule)
    {
    }

    /// <summary>
    /// Removes a previously registered converter rule. The default does nothing.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "deregisterConverterRule(RelOptPlanner, ConverterRule)")]
    public virtual void DeregisterConverterRule(IOpPlanner planner, ConverterRule converterRule)
    {
    }

}

/// <summary>
/// Strongly-typed base for a trait dimension. A def of <typeparamref name="TTrait"/> yields
/// <typeparamref name="TTrait"/> values from a <see cref="OpTraitSet"/>.
/// </summary>
/// <typeparam name="TTrait">The trait type carried on this dimension.</typeparam>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef")]
public abstract class OpTraitDef<TTrait> : OpTraitDef
    where TTrait : class, IOpTrait
{

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "getTraitClass()")]
    public override Type TraitType => typeof(TTrait);

    /// <summary>
    /// The value an op carries on this dimension when none is specified.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTraitDef", "getDefault()")]
    public abstract override TTrait Default { get; }

}
