using Alembic.Plan;

namespace Alembic.Algebra.Convert;

/// <summary>
/// An op that converts one <see cref="IOpTrait"/> of its input from one value to another without
/// changing the result. By declaring itself a converter, an op tells a cost-based planner that its
/// input and output are logically equivalent but physically different.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter")]
public interface IConverter : IOp
{

    /// <summary>
    /// The traits of the input being converted.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getInputTraits()")]
    OpTraitSet InputTraits { get; }

    /// <summary>
    /// The dimension this converter modifies (the others are preserved), or <c>null</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getTraitDef()")]
    OpTraitDef? TraitDef { get; }

    /// <summary>
    /// The sole input being converted.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getInput()")]
    IOp Input { get; }

}
