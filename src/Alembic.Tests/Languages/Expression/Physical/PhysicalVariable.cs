using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical leaf naming a variable whose value is supplied at evaluation time.
/// </summary>
sealed class PhysicalVariable : AbstractOp
{

    readonly string _name;

    public PhysicalVariable(OpCluster cluster, OpTraitSet traits, string name)
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

    public override IOpNode Copy(OpTraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new PhysicalVariable(Cluster, traits, _name);
    }

}
