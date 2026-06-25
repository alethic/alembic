using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

using Alembic.Algebra;
using Alembic.Algebra.Metadata;
using Alembic.Plan;
using Alembic.Util;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// The physical counterpart of a logical filter. Applying the filter adds a small cost on top of its
/// input. A filter preserves any physical property of its input, so it both passes a required trait set
/// down to its input and derives a delivered trait set up from it.
/// </summary>
sealed class PhysicalFilter : AbstractOp, IPhysicalOp
{

    IOp _input;
    readonly string _predicate;

    public PhysicalFilter(OpTraitSet traits, IOp input, string predicate)
        : base(input.Cluster, traits)
    {
        _input = input;
        _predicate = predicate;
    }

    public IOp Input => _input;

    public string Predicate => _predicate;

    public override ImmutableArray<IOp> Inputs => [_input];

    protected override IOutputType DeriveOutputType() => _input.OutputType;

    public override IOpCost ComputeSelfCost(IOpPlanner planner, OpMetadataQuery mq) => planner.CostFactory.MakeCost(10, 0);

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
        writer.Input("input", _input);
        writer.Item("predicate", _predicate);
        return writer;
    }

    public override void ReplaceInput(int ordinalInParent, IOp p)
    {
        Debug.Assert(ordinalInParent == 0);
        _input = p;
        RecomputeDigest();
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new PhysicalFilter(traits, children[0], _predicate);
    }

}
