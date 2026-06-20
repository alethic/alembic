using System;
using System.Collections.Immutable;

using Alembic.Plan.Traits;

namespace Alembic.Algebra;

/// <summary>
/// Convenience base for <see cref="INode"/> implementations. A node overrides the single
/// <see cref="Signature"/> member, and the base derives <see cref="DeepEquals"/> /
/// <see cref="DeepHashCode"/> from it, caching the hash.
/// </summary>
public abstract class NodeBase : INode
{

    int _hash;

    /// <summary>
    /// Initializes the node with its traits and children.
    /// </summary>
    protected NodeBase(TraitSet traits, ImmutableArray<INode> children)
    {
        Traits = traits;
        Children = children;
    }

    /// <inheritdoc />
    public TraitSet Traits { get; }

    /// <inheritdoc />
    public ImmutableArray<INode> Children { get; }

    /// <summary>
    /// This node's own identity-bearing attributes, excluding its children and traits.
    /// Two nodes of the same type with equal signatures, traits, and children are structurally
    /// equivalent.
    /// </summary>
    protected abstract object Signature { get; }

    /// <inheritdoc />
    public abstract INode Copy(TraitSet traits, ImmutableArray<INode> children);

    /// <inheritdoc />
    public virtual bool DeepEquals(INode? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || GetType() != other.GetType()) return false;

        var that = (NodeBase)other;
        if (!Traits.Equals(that.Traits)) return false;
        if (!Equals(Signature, that.Signature)) return false;
        if (Children.Length != that.Children.Length) return false;

        for (int i = 0; i < Children.Length; i++)
        {
            if (!Children[i].DeepEquals(that.Children[i])) return false;
        }

        return true;
    }

    /// <inheritdoc />
    public virtual int DeepHashCode()
    {
        if (_hash == 0)
        {
            var h = new HashCode();
            h.Add(GetType());
            h.Add(Traits);
            h.Add(Signature);
            foreach (var child in Children)
                h.Add(child.DeepHashCode());

            _hash = h.ToHashCode();
            if (_hash == 0) _hash = 1;
        }

        return _hash;
    }

}
