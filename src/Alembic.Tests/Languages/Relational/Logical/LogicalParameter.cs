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

    public LogicalParameter(OpCluster cluster, OpTraitSet traits, string name)
        : base(cluster, traits, ImmutableArray<IOp>.Empty)
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

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new LogicalParameter(Cluster, traits, _name);
    }

}
