using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Plan;
using Alembic.Util;

namespace Alembic.Algebra;

/// <summary>
/// An op that implements its convention physically and can participate in the top-down search's trait
/// derivation. A physical op may <see cref="PassThrough"/> a required trait set down to its inputs, or
/// <see cref="Derive(TraitSet, int)"/> a delivered trait set up from an already-optimized input.
/// </summary>
/// <remarks>
/// The pair returned by <see cref="PassThroughTraits"/> / <see cref="DeriveTraits"/> is
/// (the op's resulting trait set, the trait set required of each input). The default
/// <see cref="PassThrough"/> / <see cref="Derive(TraitSet, int)"/> compose that pair into a new op by
/// converting each input; ops that derive traits override the <c>…Traits</c> methods (or, for
/// <see cref="DeriveMode.Omakase"/>, <see cref="Derive(IList{IList{TraitSet}})"/>).
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.PhysicalNode")]
public interface IPhysicalNode : IOpNode
{

    /// <summary>
    /// Pushes <paramref name="required"/> down to the inputs, returning an op delivering it, or
    /// <c>null</c> if it cannot.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.PhysicalNode", "passThrough(RelTraitSet)")]
    IOpNode? PassThrough(TraitSet required)
    {
        var pair = PassThroughTraits(required);
        if (pair is null)
            return null;

        var inputs = ImmutableArray.CreateBuilder<IOpNode>(Children.Length);
        for (int i = 0; i < Children.Length; i++)
            inputs.Add(Cluster.Planner.ChangeTraits(Children[i], pair.Right[i]));

        return Copy(pair.Left, inputs.MoveToImmutable());
    }

    /// <summary>
    /// The trait set this op would deliver for <paramref name="required"/> (the pair's left), paired
    /// with the trait set each input must then deliver (the pair's right). Returns <c>null</c> if the
    /// requirement cannot be passed through.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.PhysicalNode", "passThroughTraits(RelTraitSet)")]
    Pair<TraitSet, IList<TraitSet>>? PassThroughTraits(TraitSet required)
    {
        throw new NotSupportedException(GetType().Name + " does not implement PassThroughTraits.");
    }

    /// <summary>
    /// Derives a delivered trait set from the input at <paramref name="childId"/> (which already delivers
    /// <paramref name="childTraits"/>), returning the derived op, or <c>null</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.PhysicalNode", "derive(RelTraitSet, int)")]
    IOpNode? Derive(TraitSet childTraits, int childId)
    {
        var pair = DeriveTraits(childTraits, childId);
        if (pair is null)
            return null;

        var inputs = ImmutableArray.CreateBuilder<IOpNode>(Children.Length);
        for (int i = 0; i < Children.Length; i++)
            inputs.Add(Cluster.Planner.ChangeTraits(Children[i], pair.Right[i]));

        return Copy(pair.Left, inputs.MoveToImmutable());
    }

    /// <summary>
    /// The trait set this op would deliver given the input at <paramref name="childId"/> delivers
    /// <paramref name="childTraits"/> (the pair's left), paired with the trait set each input must then
    /// deliver (the pair's right), or <c>null</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.PhysicalNode", "deriveTraits(RelTraitSet, int)")]
    Pair<TraitSet, IList<TraitSet>>? DeriveTraits(TraitSet childTraits, int childId)
    {
        throw new NotSupportedException(GetType().Name + " does not implement DeriveTraits.");
    }

    /// <summary>
    /// Given a trait set for each input, returns the derived ops. Called only under
    /// <see cref="DeriveMode.Omakase"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.PhysicalNode", "derive(List<List<RelTraitSet>>)")]
    IList<IOpNode> Derive(IList<IList<TraitSet>> inputTraits)
    {
        throw new NotSupportedException(GetType().Name + " does not implement Derive.");
    }

    /// <summary>
    /// How this op derives traits from its inputs. Defaults to <see cref="DeriveMode.LeftFirst"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.PhysicalNode", "getDeriveMode()")]
    DeriveMode DeriveMode => DeriveMode.LeftFirst;

}
