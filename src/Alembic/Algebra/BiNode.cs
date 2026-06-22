using System.Collections.Immutable;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// Convenience base for an <see cref="INode"/> with exactly two children — a left and a right. It
/// lists both as input terms; subclasses add their own attributes in <see cref="AbstractNode.Explain"/>
/// and override <see cref="INode.Copy"/>.
/// </summary>
public abstract class BiNode : AbstractNode
{

    /// <summary>
    /// Initializes the node with its traits and its left and right children.
    /// </summary>
    protected BiNode(TraitSet traits, INode left, INode right)
        : base(traits, ImmutableArray.Create(left, right))
    {

    }

    /// <summary>
    /// This node's left child.
    /// </summary>
    public INode Left => Children[0];

    /// <summary>
    /// This node's right child.
    /// </summary>
    public INode Right => Children[1];

    /// <inheritdoc />
    protected override void Explain(INodeWriter writer)
    {
        base.Explain(writer);
        writer.Input("left", Left);
        writer.Input("right", Right);
    }

}
