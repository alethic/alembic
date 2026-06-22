using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// The physical counterpart of a logical filter. Applying the filter adds a small cost on top of its
/// input.
/// </summary>
sealed class PhysicalFilter : SingleNode
{

    readonly string _predicate;

    public PhysicalFilter(TraitSet traits, INode input, string predicate)
        : base(traits, input)
    {
        _predicate = predicate;
    }

    public INode Input => Child;

    public string Predicate => _predicate;

    public override ICost ComputeSelfCost(IPlanner planner) => planner.CostFactory.MakeCost(10, 0);

    protected override void Explain(INodeWriter writer)
    {
        base.Explain(writer);
        writer.Item("predicate", _predicate);
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new PhysicalFilter(traits, children[0], _predicate);
    }

}
