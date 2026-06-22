using Alembic.Plan;

namespace Alembic.Algebra.Convert;

/// <summary>
/// Abstract base for an <see cref="IConverter"/>: a single-input node that records the input's traits
/// and the dimension it converts.
/// </summary>
public abstract class ConverterImpl : SingleNode, IConverter
{

    /// <summary>
    /// Creates a converter producing <paramref name="traits"/> from <paramref name="child"/>, modifying
    /// the dimension <paramref name="traitDef"/>.
    /// </summary>
    protected ConverterImpl(ITraitDef? traitDef, TraitSet traits, INode child)
        : base(traits, child)
    {
        InputTraits = child.Traits;
        TraitDef = traitDef;
    }

    /// <inheritdoc />
    public TraitSet InputTraits { get; }

    /// <inheritdoc />
    public ITraitDef? TraitDef { get; }

    /// <inheritdoc />
    public INode Input => Child;

}
