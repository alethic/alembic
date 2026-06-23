using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Logical;

/// <summary>
/// A logical leaf naming a variable whose value is supplied at evaluation time.
/// </summary>
sealed class Variable : AbstractOp
{

    readonly string _name;

    public Variable(OpCluster cluster, OpTraitSet traits, string name)
        : base(cluster, traits)
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
        return new Variable(Cluster, traits, _name);
    }

}
