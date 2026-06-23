using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Algebra.Convert;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Moves an image from the CPU to the GPU — the enforcer that bridges a CPU result to a GPU consumer.
/// Being a converter, the planner does not push the GPU convention onto its input, so its input stays
/// on the CPU.
/// </summary>
sealed class Upload : ConverterImpl
{

    public Upload(TraitSet traits, IOpNode input)
        : base(ConventionTraitDef.Instance, traits, input)
    {

    }

    public override ICost ComputeSelfCost(IPlanner planner)
    {
        return planner.CostFactory.MakeCost(ImageConventions.TransferCost, 0);
    }

    public override IOpNode Copy(TraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new Upload(traits, children[0]);
    }

}
