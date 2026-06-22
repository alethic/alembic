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

    public LogicalSource(TraitSet traits, string table)
        : base(traits, ImmutableArray<INode>.Empty)
    {
        _table = table;
    }

    public string Table => _table;

    protected override void Explain(INodeWriter writer)
    {
        writer.Item("table", _table);
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new LogicalSource(traits, _table);
    }

}
