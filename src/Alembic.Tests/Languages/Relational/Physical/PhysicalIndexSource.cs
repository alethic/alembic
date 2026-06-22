using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// A cheaper physical scan that reads through an index rather than scanning the whole source — an
/// alternative realization of a source scan.
/// </summary>
sealed class PhysicalIndexSource : AbstractNode
{

    readonly string _table;

    public PhysicalIndexSource(Cluster cluster, TraitSet traits, string table)
        : base(cluster, traits, ImmutableArray<INode>.Empty)
    {
        _table = table;
    }

    public string Table => _table;

    public override ICost ComputeSelfCost(IPlanner planner) => planner.CostFactory.MakeCost(50, 0);

    public override INodeWriter ExplainTerms(INodeWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("table", _table);
        return writer;
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new PhysicalIndexSource(Cluster, traits, _table);
    }

}
