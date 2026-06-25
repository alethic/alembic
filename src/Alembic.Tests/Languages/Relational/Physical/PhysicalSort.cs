using System.Collections.Immutable;
using System.Diagnostics;

using Alembic.Algebra;
using Alembic.Algebra.Metadata;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// A physical enforcer that delivers its input in sorted order. It is how the planner satisfies a
/// required sortedness that the input does not already provide, and it costs a little on top of its
/// input.
/// </summary>
sealed class PhysicalSort : AbstractOp
{

    IOp _input;

    public PhysicalSort(OpTraitSet traits, IOp input)
        : base(input.Cluster, traits)
    {
        _input = input;
    }

    public IOp Input => _input;

    public override ImmutableArray<IOp> Inputs => [_input];

    protected override IOutputType DeriveOutputType() => _input.OutputType;

    public override IOpCost ComputeSelfCost(IOpPlanner planner, OpMetadataQuery mq) => planner.CostFactory.MakeCost(50, 0);

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("input", _input);
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
        return new PhysicalSort(traits, children[0]);
    }

}
