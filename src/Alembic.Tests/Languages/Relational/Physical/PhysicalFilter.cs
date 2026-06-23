using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Util;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// The physical counterpart of a logical filter. Applying the filter adds a small cost on top of its
/// input. A filter preserves any physical property of its input, so it both passes a required trait set
/// down to its input and derives a delivered trait set up from it.
/// </summary>
sealed class PhysicalFilter : SingleOp, IPhysicalNode
{

    readonly string _predicate;

    public PhysicalFilter(TraitSet traits, IOpNode input, string predicate)
        : base(traits, input)
    {
        _predicate = predicate;
    }

    public IOpNode Input => Child;

    public string Predicate => _predicate;

    public override ICost ComputeSelfCost(IPlanner planner) => planner.CostFactory.MakeCost(10, 0);

    public Pair<TraitSet, IList<TraitSet>>? PassThroughTraits(TraitSet required)
    {
        return Pair.Of(required, (IList<TraitSet>)new[] { required });
    }

    public Pair<TraitSet, IList<TraitSet>>? DeriveTraits(TraitSet childTraits, int childId)
    {
        return Pair.Of(childTraits, (IList<TraitSet>)new[] { childTraits });
    }

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("predicate", _predicate);
        return writer;
    }

    public override IOpNode Copy(TraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new PhysicalFilter(traits, children[0], _predicate);
    }

}
