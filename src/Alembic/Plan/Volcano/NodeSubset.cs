using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The members of a <see cref="NodeSet"/> that share one trait set — equivalent plans with identical
/// physical properties. A subset is itself an <see cref="INode"/> so it can stand in as a child of a
/// registered node, and it remembers the cheapest member found so far.
/// </summary>
public sealed class NodeSubset : AbstractNode
{

    internal NodeSubset(NodeSet set, TraitSet traits, ICost infiniteCost)
        : base(traits, ImmutableArray<INode>.Empty)
    {
        Set = set;
        BestCost = infiniteCost;
    }

    /// <summary>
    /// The equivalence set this subset belongs to.
    /// </summary>
    public NodeSet Set { get; }

    /// <summary>
    /// The cheapest member found so far, or <c>null</c> if none has a finite cost yet.
    /// </summary>
    public INode? Best { get; internal set; }

    /// <summary>
    /// The cost of <see cref="Best"/> (infinite until a member is costed).
    /// </summary>
    public ICost BestCost { get; internal set; }

    /// <summary>
    /// A subset's identity is its set; the trait set is compared by the base. (<see cref="Best"/> is
    /// mutable optimizer state and is deliberately excluded.)
    /// </summary>
    protected override void Explain(INodeWriter writer)
    {
        writer.Item("subset", Set.Id);
    }

    /// <inheritdoc />
    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return this;
    }

}
