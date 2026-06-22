using System;

using Alembic.Algebra;

namespace Alembic.Plan;

/// <summary>
/// A calling convention — the "family" a node belongs to (logical, or some physical backend).
/// Converter rules move nodes between conventions; a fully lowered plan is one whose nodes are all in
/// a target convention. Its default implementation is <see cref="Convention"/>.
/// </summary>
public interface IConvention : ITrait
{

    /// <summary>
    /// The convention of a node that does not support any convention. It is not implementable and must
    /// be transformed to something else; such nodes have infinite cost. Nodes generally start here.
    /// </summary>
    static IConvention None => Convention.None;

    /// <summary>
    /// This convention's name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The node interface that members of this convention are expected to implement. Defaults to
    /// <see cref="INode"/> for conventions that impose no marker.
    /// </summary>
    Type Interface { get; }

    /// <summary>
    /// Whether the planner should convert from this convention to <paramref name="toConvention"/>.
    /// </summary>
    bool CanConvertConvention(IConvention toConvention) => false;

    /// <summary>
    /// Whether the planner should add abstract converters to bridge from <paramref name="fromTraits"/>
    /// to <paramref name="toTraits"/>. A convention opts in to handling each trait's conversion here.
    /// </summary>
    bool UseAbstractConvertersForConversion(TraitSet fromTraits, TraitSet toTraits) => false;

    /// <summary>
    /// Produces an enforcer node that delivers <paramref name="input"/> with the required traits (a
    /// physical sort, exchange, etc.), or <c>null</c> if enforcement is not allowed. The default is
    /// unimplemented.
    /// </summary>
    INode? Enforce(INode input, TraitSet required)
    {
        throw new NotImplementedException($"{GetType().Name}.Enforce is not implemented.");
    }

}
