using System.Collections.Immutable;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// Convenience base for an <see cref="INode"/> with exactly one child. It lists the child as an input
/// term; subclasses add their own attributes in <see cref="AbstractNode.Explain"/> and override
/// <see cref="INode.Copy"/>.
/// </summary>
public abstract class SingleNode : AbstractNode
{

    /// <summary>
    /// Initializes the node with its traits and single child.
    /// </summary>
    protected SingleNode(TraitSet traits, INode child)
        : base(traits, ImmutableArray.Create(child))
    {

    }

    /// <summary>
    /// This node's single child.
    /// </summary>
    public INode Child => Children[0];

    /// <inheritdoc />
    protected override void Explain(INodeWriter writer)
    {
        base.Explain(writer);
        writer.Input("input", Child);
    }

}
