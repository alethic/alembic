using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Convenience base for a single-input image operation. Its self-cost is the operation cost of
/// whatever convention it currently carries, so the same operation is cheap on the GPU and dear on the
/// CPU.
/// </summary>
abstract class ImageOp : SingleOp, IImageOperation
{

    protected ImageOp(TraitSet traits, IOpNode input)
        : base(traits, input)
    {

    }

    public IOpNode Input => Child;

    /// <summary>
    /// Whether this operation has a GPU implementation. Most do; CPU-only operations override this.
    /// </summary>
    public virtual bool SupportsGpu => true;

    public override ICost ComputeSelfCost(IPlanner planner)
    {
        return planner.CostFactory.MakeCost(ImageConventions.OpCost(Traits.Convention), 0);
    }

}
