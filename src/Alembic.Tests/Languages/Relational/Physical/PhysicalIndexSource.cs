using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// A cheaper physical scan that reads through an index rather than scanning the whole source — an
/// alternative realization of a source scan.
/// </summary>
sealed class PhysicalIndexSource : AbstractOp
{

    readonly string _table;

    public PhysicalIndexSource(OpCluster cluster, OpTraitSet traits, string table)
        : base(cluster, traits, ImmutableArray<IOpNode>.Empty)
    {
        _table = table;
    }

    public string Table => _table;

    public override IOpCost ComputeSelfCost(IOpPlanner planner) => planner.CostFactory.MakeCost(50, 0);

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("table", _table);
        return writer;
    }

    public override IOpNode Copy(OpTraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new PhysicalIndexSource(Cluster, traits, _table);
    }

}
