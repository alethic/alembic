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
sealed class PhysicalFilter : SingleOp, IPhysicalOp
{

    readonly string _predicate;

    public PhysicalFilter(OpTraitSet traits, IOp input, string predicate)
        : base(input.Cluster, traits, input)
    {
        _predicate = predicate;
    }

    public IOp Input => Child;

    public string Predicate => _predicate;

    public override IOpCost ComputeSelfCost(IOpPlanner planner) => planner.CostFactory.MakeCost(10, 0);

    public Pair<OpTraitSet, IList<OpTraitSet>>? PassThroughTraits(OpTraitSet required)
    {
        return Pair.Of(required, (IList<OpTraitSet>)new[] { required });
    }

    public Pair<OpTraitSet, IList<OpTraitSet>>? DeriveTraits(OpTraitSet childTraits, int childId)
    {
        return Pair.Of(childTraits, (IList<OpTraitSet>)new[] { childTraits });
    }

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("predicate", _predicate);
        return writer;
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new PhysicalFilter(traits, children[0], _predicate);
    }

}
