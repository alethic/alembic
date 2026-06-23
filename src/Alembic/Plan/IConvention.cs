using System;

using Alembic.Algebra;

namespace Alembic.Plan;

/// <summary>
/// A calling convention — the "family" an op belongs to (logical, or some physical backend).
/// Converter rules move ops between conventions; a fully lowered plan is one whose ops are all in
/// a target convention. Its default implementation is <see cref="Convention"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention")]
public interface IConvention : IOpTrait
{

    /// <summary>
    /// The convention of an op that does not support any convention. It is not implementable and must
    /// be transformed to something else; such ops have infinite cost. Ops generally start here.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention", "NONE")]
    static IConvention None => Convention.None;

    /// <summary>
    /// This convention's name.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention", "getName()")]
    string Name { get; }

    /// <summary>
    /// The op interface that members of this convention are expected to implement. Defaults to
    /// <see cref="IOp"/> for conventions that impose no marker.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention", "getInterface()")]
    Type Interface { get; }

    /// <summary>
    /// Whether the planner should convert from this convention to <paramref name="toConvention"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention", "canConvertConvention(Convention)")]
    bool CanConvertConvention(IConvention toConvention) => false;

    /// <summary>
    /// Whether the planner should add abstract converters to bridge from <paramref name="fromTraits"/>
    /// to <paramref name="toTraits"/>. A convention opts in to handling each trait's conversion here.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention", "useAbstractConvertersForConversion(RelTraitSet, RelTraitSet)")]
    bool UseAbstractConvertersForConversion(OpTraitSet fromTraits, OpTraitSet toTraits) => false;

    /// <summary>
    /// Produces an enforcer op that delivers <paramref name="input"/> with the required traits (a
    /// physical sort, exchange, etc.), or <c>null</c> if enforcement is not allowed. The default is
    /// unimplemented.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention", "enforce(RelNode, RelTraitSet)")]
    IOp? Enforce(IOp input, OpTraitSet required)
    {
        throw new NotImplementedException($"{GetType().Name}.Enforce is not implemented.");
    }

}
