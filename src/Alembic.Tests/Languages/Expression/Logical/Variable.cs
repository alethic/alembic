using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Logical;

/// <summary>
/// A logical leaf naming a variable whose value is supplied at evaluation time.
/// </summary>
sealed class Variable : AbstractNode
{

    readonly string _name;

    public Variable(Cluster cluster, TraitSet traits, string name)
        : base(cluster, traits, ImmutableArray<INode>.Empty)
    {
        _name = name;
    }

    public string Name => _name;

    public override INodeWriter ExplainTerms(INodeWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("name", _name);
        return writer;
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new Variable(Cluster, traits, _name);
    }

}
