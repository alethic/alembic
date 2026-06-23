using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Logical;

/// <summary>
/// A logical leaf standing for a bound parameter or relation variable (an input supplied by name
/// rather than a named table).
/// </summary>
sealed class LogicalParameter : AbstractOp
{

    readonly string _name;

    public LogicalParameter(Cluster cluster, TraitSet traits, string name)
        : base(cluster, traits, ImmutableArray<IOpNode>.Empty)
    {
        _name = name;
    }

    public string Name => _name;

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("name", _name);
        return writer;
    }

    public override IOpNode Copy(TraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new LogicalParameter(Cluster, traits, _name);
    }

}
