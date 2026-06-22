using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Algebra.Convert;

namespace Alembic.Plan.Volcano;

/// <summary>
/// A placeholder that demands its input be delivered with a different trait set. It is always
/// unimplementable on its own — its cost is infinite — so the planner must replace it, via
/// <see cref="ExpandConversionRule"/>, with a real chain of converters. This is how a cost-based
/// planner reaches a required output convention.
/// </summary>
public sealed class AbstractConverter : ConverterImpl
{

    /// <summary>
    /// Creates a converter that requires <paramref name="input"/> in the given target traits.
    /// </summary>
    public AbstractConverter(TraitSet target, INode input, ITraitDef? traitDef = null)
        : base(traitDef, target, input)
    {

    }

    /// <inheritdoc />
    public override INodeWriter ExplainTerms(INodeWriter writer)
    {
        base.ExplainTerms(writer);
        foreach (var trait in Traits)
            writer.Item(trait.TraitDef.Name, trait);

        return writer;
    }

    /// <inheritdoc />
    public override ICost ComputeSelfCost(IPlanner planner) => planner.CostFactory.MakeInfiniteCost();

    /// <inheritdoc />
    public bool IsEnforcer => true;

    /// <inheritdoc />
    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new AbstractConverter(traits, children[0], TraitDef);
    }

}
