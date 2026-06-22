using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// The physical counterpart of a logical source. Scanning the source has a fixed cost.
/// </summary>
sealed class PhysicalSource : AbstractNode
{

    readonly string _table;

    public PhysicalSource(TraitSet traits, string table)
        : base(traits, ImmutableArray<INode>.Empty)
    {
        _table = table;
    }

    public string Table => _table;

    public override ICost ComputeSelfCost(IPlanner planner) => planner.CostFactory.MakeCost(100, 0);

    protected override void Explain(INodeWriter writer)
    {
        writer.Item("table", _table);
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new PhysicalSource(traits, _table);
    }

}
