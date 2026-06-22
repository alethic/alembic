using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Plan.Rules;

/// <summary>
/// A convenience base class for converter rules: it holds the <see cref="Source"/> and
/// <see cref="Target"/> traits, builds the matching <see cref="Operand"/> once, and leaves
/// <see cref="Convert"/> to the subclass. The match action is inherited from
/// <see cref="IConverterRule"/>.
/// </summary>
public abstract class ConverterRule : IConverterRule
{

    readonly Operand _operand;

    /// <summary>
    /// Initializes the rule with the traits it converts between.
    /// </summary>
    protected ConverterRule(ITrait source, ITrait target)
    {
        Source = source;
        Target = target;
        _operand = new Operand(node => node.Traits.Get(source.Def).Equals(source));
    }

    /// <inheritdoc />
    public ITrait Source { get; }

    /// <inheritdoc />
    public ITrait Target { get; }

    /// <inheritdoc />
    public Operand Operand => _operand;

    /// <inheritdoc />
    public abstract INode? Convert(INode node);

}
