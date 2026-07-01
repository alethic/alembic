using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Benchmarks;

/// <summary>
/// A leaf op carrying a string-valued term (<c>name</c>) and a value-typed term (<c>index</c>). The
/// value-typed term exercises the boxing that once happened when terms were stored as <c>object?</c>.
/// </summary>
sealed class Var : AbstractOp
{

    readonly string _name;
    readonly int _index;

    public Var(OpCluster cluster, OpTraitSet traits, string name, int index)
        : base(cluster, traits)
    {
        _name = name;
        _index = index;
    }

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("name", _name);
        writer.Item("index", _index);
        return writer;
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
        => new Var(Cluster, traits, _name, _index);

}
