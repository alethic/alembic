using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// A rule that converts a node from one trait value to another — most commonly a convention, but any
/// trait (sortedness, distribution, …). The author supplies <see cref="Source"/>, <see cref="Target"/>,
/// and <see cref="Convert"/>; the operand (matching any node carrying the <see cref="Source"/> trait) and
/// the match action are provided here. <see cref="Convert"/> returns the converted node, or null to
/// decline (when it does not handle this node's kind).
/// </summary>
public abstract class ConverterRule : Rule
{

    /// <summary>
    /// Initializes the rule with the traits it converts between.
    /// </summary>
    protected ConverterRule(ITrait source, ITrait target)
        : base(ConvertOperand<INode>(source))
    {
        Source = source;
        Target = target;
    }

    /// <summary>
    /// The trait this rule converts from.
    /// </summary>
    public ITrait Source { get; }

    /// <summary>
    /// The trait this rule converts to.
    /// </summary>
    public ITrait Target { get; }

    /// <summary>
    /// Whether this converter always produces its <see cref="Target"/> (most do). A non-guaranteed
    /// converter is applied bottom-up, via a <see cref="TraitMatchingRule"/>, only where its input is
    /// already in the target form.
    /// </summary>
    public virtual bool IsGuaranteed => true;

    /// <summary>
    /// Converts the node from <see cref="Source"/> to <see cref="Target"/>, or returns null to decline.
    /// </summary>
    public abstract INode? Convert(INode node);

    /// <inheritdoc />
    public override void OnMatch(RuleCall call)
    {
        var converted = Convert(call.Node(0));
        if (converted is not null)
            call.TransformTo(converted);
    }

}
