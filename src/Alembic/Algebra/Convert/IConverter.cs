using Alembic.Plan;

namespace Alembic.Algebra.Convert;

/// <summary>
/// A node that converts one <see cref="ITrait"/> of its input from one value to another without
/// changing the result. By declaring itself a converter, a node tells a cost-based planner that its
/// input and output are logically equivalent but physically different.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter")]
public interface IConverter : INode
{

    /// <summary>
    /// The traits of the input being converted.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getInputTraits()")]
    TraitSet InputTraits { get; }

    /// <summary>
    /// The dimension this converter modifies (the others are preserved), or <c>null</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getTraitDef()")]
    TraitDef? TraitDef { get; }

    /// <summary>
    /// The sole input being converted.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.Converter", "getInput()")]
    INode Input { get; }

}
