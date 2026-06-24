using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Algebra.Convert;
using Alembic.Algebra.Metadata;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Moves an image from the CPU to the GPU — the enforcer that bridges a CPU result to a GPU consumer.
/// Being a converter, the planner does not push the GPU convention onto its input, so its input stays
/// on the CPU.
/// </summary>
sealed class Upload : ConverterImpl
{

    public Upload(OpTraitSet traits, IOp input)
        : base(input.Cluster, ConventionTraitDef.Instance, traits, input)
    {

    }

    public override IOpCost ComputeSelfCost(IOpPlanner planner, OpMetadataQuery mq)
    {
        return planner.CostFactory.MakeCost(ImageConventions.TransferCost, 0);
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new Upload(traits, children[0]);
    }

}
