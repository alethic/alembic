using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// A physical enforcer that delivers its input in sorted order. It is how the planner satisfies a
/// required sortedness that the input does not already provide, and it costs a little on top of its
/// input.
/// </summary>
sealed class PhysicalSort : SingleOp
{

    public PhysicalSort(TraitSet traits, IOpNode input)
        : base(traits, input)
    {

    }

    public IOpNode Input => Child;

    public override ICost ComputeSelfCost(IPlanner planner) => planner.CostFactory.MakeCost(50, 0);

    public override IOpNode Copy(TraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new PhysicalSort(traits, children[0]);
    }

}
