using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Plan;
using Alembic.Util;

namespace Alembic.Algebra;

/// <summary>
/// A node that implements its convention physically and can participate in the top-down search's trait
/// derivation. A physical node may <see cref="PassThrough"/> a required trait set down to its inputs, or
/// <see cref="Derive(TraitSet, int)"/> a delivered trait set up from an already-optimized input.
/// </summary>
/// <remarks>
/// The pair returned by <see cref="PassThroughTraits"/> / <see cref="DeriveTraits"/> is
/// (the node's resulting trait set, the trait set required of each input). The default
/// <see cref="PassThrough"/> / <see cref="Derive(TraitSet, int)"/> compose that pair into a new node by
/// converting each input; nodes that derive traits override the <c>…Traits</c> methods (or, for
/// <see cref="DeriveMode.Omakase"/>, <see cref="Derive(IList{IList{TraitSet}})"/>).
/// </remarks>
public interface IPhysicalNode : INode
{

    /// <summary>
    /// Pushes <paramref name="required"/> down to the inputs, returning a node delivering it, or
    /// <c>null</c> if it cannot.
    /// </summary>
    INode? PassThrough(TraitSet required)
    {
        var pair = PassThroughTraits(required);
        if (pair is null)
            return null;

        var inputs = ImmutableArray.CreateBuilder<INode>(Children.Length);
        for (int i = 0; i < Children.Length; i++)
            inputs.Add(Cluster.Planner.Convert(Children[i], pair.Right[i]));

        return Copy(pair.Left, inputs.MoveToImmutable());
    }

    /// <summary>
    /// The trait set this node would deliver for <paramref name="required"/> (the pair's left), paired
    /// with the trait set each input must then deliver (the pair's right). Returns <c>null</c> if the
    /// requirement cannot be passed through.
    /// </summary>
    Pair<TraitSet, IList<TraitSet>>? PassThroughTraits(TraitSet required)
    {
        throw new NotSupportedException(GetType().Name + " does not implement PassThroughTraits.");
    }

    /// <summary>
    /// Derives a delivered trait set from the input at <paramref name="childId"/> (which already delivers
    /// <paramref name="childTraits"/>), returning the derived node, or <c>null</c>.
    /// </summary>
    INode? Derive(TraitSet childTraits, int childId)
    {
        var pair = DeriveTraits(childTraits, childId);
        if (pair is null)
            return null;

        var inputs = ImmutableArray.CreateBuilder<INode>(Children.Length);
        for (int i = 0; i < Children.Length; i++)
            inputs.Add(Cluster.Planner.Convert(Children[i], pair.Right[i]));

        return Copy(pair.Left, inputs.MoveToImmutable());
    }

    /// <summary>
    /// The trait set this node would deliver given the input at <paramref name="childId"/> delivers
    /// <paramref name="childTraits"/> (the pair's left), paired with the trait set each input must then
    /// deliver (the pair's right), or <c>null</c>.
    /// </summary>
    Pair<TraitSet, IList<TraitSet>>? DeriveTraits(TraitSet childTraits, int childId)
    {
        throw new NotSupportedException(GetType().Name + " does not implement DeriveTraits.");
    }

    /// <summary>
    /// Given a trait set for each input, returns the derived nodes. Called only under
    /// <see cref="DeriveMode.Omakase"/>.
    /// </summary>
    IList<INode> Derive(IList<IList<TraitSet>> inputTraits)
    {
        throw new NotSupportedException(GetType().Name + " does not implement Derive.");
    }

    /// <summary>
    /// How this node derives traits from its inputs. Defaults to <see cref="DeriveMode.LeftFirst"/>.
    /// </summary>
    DeriveMode DeriveMode => DeriveMode.LeftFirst;

}
