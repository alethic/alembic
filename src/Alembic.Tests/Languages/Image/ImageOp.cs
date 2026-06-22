using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Convenience base for a single-input image operation. Its self-cost is the operation cost of
/// whatever convention it currently carries, so the same operation is cheap on the GPU and dear on the
/// CPU.
/// </summary>
abstract class ImageOp : SingleNode
{

    protected ImageOp(TraitSet traits, INode input)
        : base(traits, input)
    {

    }

    public INode Input => Child;

    public override ICost ComputeSelfCost(IPlanner planner)
    {
        return planner.CostFactory.MakeCost(ImageConventions.OpCost(Traits.Convention), 0);
    }

}
