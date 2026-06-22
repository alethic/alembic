using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// A physical source that applies a filter predicate itself — the result of pushing a
/// <see cref="PhysicalFilter"/> down into a <see cref="PhysicalSource"/>. It is a second physical
/// realization of the "filter over a scan" situation (the other being a separate filter over a
/// plain source); choosing between them is a cost-based concern for a future Volcano planner.
/// </summary>
sealed class PhysicalFilteredSource : AbstractNode
{

    readonly string _table;
    readonly string _predicate;

    public PhysicalFilteredSource(Cluster cluster, TraitSet traits, string table, string predicate)
        : base(cluster, traits, ImmutableArray<INode>.Empty)
    {
        _table = table;
        _predicate = predicate;
    }

    public string Table => _table;

    public string Predicate => _predicate;

    // A fused scan-and-filter is cheaper than a separate filter over a separate scan (10 + 100).
    public override ICost ComputeSelfCost(IPlanner planner) => planner.CostFactory.MakeCost(100, 0);

    public override INodeWriter ExplainTerms(INodeWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("table", _table);
        writer.Item("predicate", _predicate);
        return writer;
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new PhysicalFilteredSource(Cluster, traits, _table, _predicate);
    }

}
