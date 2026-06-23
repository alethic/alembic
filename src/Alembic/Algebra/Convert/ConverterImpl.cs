using Alembic.Plan;

namespace Alembic.Algebra.Convert;

/// <summary>
/// Abstract base for an <see cref="IConverter"/>: a single-input node that records the input's traits
/// and the dimension it converts.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterImpl")]
public abstract class ConverterImpl : SingleNode, IConverter
{

    /// <summary>
    /// Creates a converter producing <paramref name="traits"/> from <paramref name="child"/>, modifying
    /// the dimension <paramref name="traitDef"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterImpl", "ConverterImpl(RelOptCluster, RelTraitDef, RelTraitSet, RelNode)")]
    protected ConverterImpl(TraitDef? traitDef, TraitSet traits, INode child)
        : base(traits, child)
    {
        InputTraits = child.Traits;
        TraitDef = traitDef;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getInputTraits()")]
    public TraitSet InputTraits { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getTraitDef()")]
    public TraitDef? TraitDef { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getInput()")]
    public INode Input => Child;

}
