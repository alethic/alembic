using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// A rule that converts a node from one trait value to another — most commonly a convention, but any
/// trait (sortedness, distribution, …). The author supplies <see cref="Source"/>, <see cref="Target"/>,
/// and <see cref="Convert"/>; the operand (matching any node carrying the <see cref="Source"/> trait)
/// and the match action are provided here as a mixin. <see cref="Convert"/> returns the converted node,
/// or null to decline (when it does not handle this node's kind).
/// </summary>
public interface IConverterRule : IRule
{

    /// <summary>
    /// The trait this rule converts from.
    /// </summary>
    ITrait Source { get; }

    /// <summary>
    /// The trait this rule converts to.
    /// </summary>
    ITrait Target { get; }

    /// <summary>
    /// Converts the node from <see cref="Source"/> to <see cref="Target"/>, or returns null to decline.
    /// </summary>
    INode? Convert(INode node);

    Operand IRule.Operand => new Operand(node => node.Traits.Get(Source.Def).Equals(Source));

    void IRule.OnMatch(RuleCall call)
    {
        var converted = Convert(call.Node(0));
        if (converted is not null)
            call.Transform(converted);
    }

}
