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

    public LogicalSource(Cluster cluster, TraitSet traits, string table)
        : base(cluster, traits, ImmutableArray<IOpNode>.Empty)
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

    public override IOpNode Copy(TraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new LogicalSource(Cluster, traits, _table);
    }

}
