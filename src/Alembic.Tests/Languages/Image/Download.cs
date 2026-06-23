using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Algebra.Convert;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Moves an image from the GPU to the CPU — the enforcer that bridges a GPU result to a CPU consumer.
/// Being a converter, the planner does not push the CPU convention onto its input, so its input stays
/// on the GPU.
/// </summary>
sealed class Download : ConverterImpl
{

    public Download(OpTraitSet traits, IOpNode input)
        : base(ConventionTraitDef.Instance, traits, input)
    {

    }

    public override IOpCost ComputeSelfCost(IOpPlanner planner)
    {
        return planner.CostFactory.MakeCost(ImageConventions.TransferCost, 0);
    }

    public override IOpNode Copy(OpTraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new Download(traits, children[0]);
    }

}
