using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// The physical counterpart of a logical parameter.
/// </summary>
sealed class PhysicalParameter : AbstractNode
{

    readonly string _name;

    public PhysicalParameter(Cluster cluster, TraitSet traits, string name)
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
        return new PhysicalParameter(Cluster, traits, _name);
    }

}
