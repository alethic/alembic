using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Logical;

/// <summary>
/// A logical leaf naming some source relation.
/// </summary>
sealed class LogicalSource : AbstractNode
{

    readonly string _table;

    public LogicalSource(Cluster cluster, TraitSet traits, string table)
        : base(cluster, traits, ImmutableArray<INode>.Empty)
    {
        _table = table;
    }

    public string Table => _table;

    public override INodeWriter ExplainTerms(INodeWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("table", _table);
        return writer;
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new LogicalSource(Cluster, traits, _table);
    }

}
