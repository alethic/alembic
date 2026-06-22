using System;
using System.Collections.Concurrent;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan;

/// <summary>
/// Strongly-typed base for a trait dimension. A def of <typeparamref name="TTrait"/> yields
/// <typeparamref name="TTrait"/> values from a <see cref="TraitSet"/>.
/// </summary>
/// <typeparam name="TTrait">The trait type carried on this dimension.</typeparam>
public abstract class TraitDef<TTrait> : ITraitDef
    where TTrait : ITrait
{

    readonly ConcurrentDictionary<ITrait, ITrait> _interned = new ConcurrentDictionary<ITrait, ITrait>();

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public Type TraitClass => typeof(TTrait);

    /// <summary>
    /// The value a node carries on this dimension when none is specified.
    /// </summary>
    public abstract TTrait Default { get; }

    ITrait ITraitDef.Default => Default;

    /// <inheritdoc />
    public virtual bool Multiple => false;

    /// <inheritdoc />
    public ITrait Canonize(ITrait trait)
    {
        return _interned.GetOrAdd(trait, trait);
    }

    /// <inheritdoc />
    public virtual bool CanConvert(IPlanner planner, ITrait fromTrait, ITrait toTrait)
    {
        return false;
    }

    /// <inheritdoc />
    public virtual INode? Convert(IPlanner planner, INode node, ITrait toTrait, bool allowInfiniteCostConverters)
    {
        return null;
    }

    /// <inheritdoc />
    public virtual void RegisterConverterRule(IPlanner planner, ConverterRule converterRule)
    {
    }

    /// <inheritdoc />
    public virtual void DeregisterConverterRule(IPlanner planner, ConverterRule converterRule)
    {
    }

}
