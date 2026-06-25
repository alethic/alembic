using System.Collections.Immutable;
using System.Diagnostics;

using Alembic.Algebra;
using Alembic.Algebra.Metadata;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Convenience base for a single-input image operation. Its self-cost is the operation cost of
/// whatever convention it currently carries, so the same operation is cheap on the GPU and dear on the
/// CPU.
/// </summary>
abstract class ImageOp : AbstractOp, IImageOperation
{

    IOp _input;

    protected ImageOp(OpTraitSet traits, IOp input)
        : base(input.Cluster, traits)
    {
        _input = input;
    }

    public IOp Input => _input;

    public override ImmutableArray<IOp> Inputs => [_input];

    protected override IOutputType DeriveOutputType() => _input.OutputType;

    /// <summary>
    /// Whether this operation has a GPU implementation. Most do; CPU-only operations override this.
    /// </summary>
    public virtual bool SupportsGpu => true;

    public override IOpCost ComputeSelfCost(IOpPlanner planner, OpMetadataQuery mq)
    {
        return planner.CostFactory.MakeCost(ImageConventions.OpCost(Traits.Convention), 0);
    }

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

}
