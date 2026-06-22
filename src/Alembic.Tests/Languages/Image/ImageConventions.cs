using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// The conventions of the image-processing language: a logical form the user writes, and two physical
/// backends — CPU and GPU. The same operation can run on either; the planner chooses per operation by
/// cost. GPU operations are cheap, CPU operations dear, and moving an image between the two
/// (<see cref="TransferCost"/>) is a middling cost the planner must weigh.
/// </summary>
static class ImageConventions
{

    public static readonly Convention Logical = new Convention("IMG-LOGICAL");

    public static readonly Convention Cpu = new Convention("IMG-CPU");

    public static readonly Convention Gpu = new Convention("IMG-GPU");

    /// <summary>
    /// The cost of moving an image between the CPU and the GPU.
    /// </summary>
    public const double TransferCost = 5;

    /// <summary>
    /// The self-cost of an operation in the given convention. Logical operations are not directly
    /// runnable, so they are infinitely expensive until lowered.
    /// </summary>
    public static double OpCost(IConvention convention)
    {
        if (convention.Equals(Gpu))
            return 1;
        if (convention.Equals(Cpu))
            return 10;

        return double.PositiveInfinity;
    }

}
