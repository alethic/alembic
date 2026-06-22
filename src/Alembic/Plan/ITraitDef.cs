using System;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan;

/// <summary>
/// A trait dimension — the set of mutually exclusive values a node may carry along one axis
/// (e.g. convention). Defs are singletons and are registered with the planner
/// (<see cref="IPlanner.AddTraitDef"/>), which builds the empty trait set from them.
/// </summary>
public interface ITraitDef
{

    /// <summary>
    /// A stable name for this dimension.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The type of the traits on this dimension.
    /// </summary>
    Type TraitClass { get; }

    /// <summary>
    /// The value a node carries on this dimension when none is specified.
    /// </summary>
    ITrait Default { get; }

    /// <summary>
    /// Whether a node may carry several values on this dimension at once (folded into a composite). The
    /// default is no.
    /// </summary>
    bool Multiple => false;

    /// <summary>
    /// Returns the canonical (interned) instance equal to <paramref name="trait"/>, so equal traits share
    /// one object and can be compared by reference.
    /// </summary>
    ITrait Canonize(ITrait trait);

    /// <summary>
    /// Whether this dimension can convert <paramref name="fromTrait"/> to <paramref name="toTrait"/>
    /// itself (an alternative to a registered converter rule). The default is no.
    /// </summary>
    bool CanConvert(IPlanner planner, ITrait fromTrait, ITrait toTrait)
    {
        return false;
    }

    /// <summary>
    /// Converts <paramref name="node"/> to <paramref name="toTrait"/> on this dimension, returning the
    /// converted node (e.g. wrapping it in an enforcer) or <c>null</c> to decline. Consulted only when
    /// <see cref="CanConvert"/> allows it. <paramref name="allowInfiniteCostConverters"/> permits a
    /// converter even when it would carry an infinite cost.
    /// </summary>
    INode? Convert(IPlanner planner, INode node, ITrait toTrait, bool allowInfiniteCostConverters)
    {
        return null;
    }

    /// <summary>
    /// Registers a converter rule that operates on this dimension. The default does nothing.
    /// </summary>
    void RegisterConverterRule(IPlanner planner, ConverterRule converterRule)
    {
    }

    /// <summary>
    /// Removes a previously registered converter rule. The default does nothing.
    /// </summary>
    void DeregisterConverterRule(IPlanner planner, ConverterRule converterRule)
    {
    }

}
