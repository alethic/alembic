using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Logical;

/// <summary>
/// A logical leaf naming some source relation.
/// </summary>
sealed class LogicalSource : AbstractOp
{

    readonly string _table;

    public LogicalSource(OpCluster cluster, OpTraitSet traits, string table)
        : base(cluster, traits, ImmutableArray<IOp>.Empty)
    {
        _table = table;
    }

    public string Table => _table;

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("table", _table);
        return writer;
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new LogicalSource(Cluster, traits, _table);
    }

}
