using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// The physical counterpart of a logical parameter.
/// </summary>
sealed class PhysicalParameter : AbstractOp
{

    readonly string _name;

    public PhysicalParameter(OpCluster cluster, OpTraitSet traits, string name)
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
        return new PhysicalParameter(Cluster, traits, _name);
    }

}
