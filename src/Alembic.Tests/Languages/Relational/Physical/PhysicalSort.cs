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

    public PhysicalSort(OpTraitSet traits, IOp input)
        : base(input.Cluster, traits, input)
    {

    }

    public IOp Input => Child;

    public override IOpCost ComputeSelfCost(IOpPlanner planner) => planner.CostFactory.MakeCost(50, 0);

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new PhysicalSort(traits, children[0]);
    }

}
