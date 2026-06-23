using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Util;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// The physical counterpart of a logical source. Scanning the source has a fixed cost. As a physical
/// op it can natively deliver any requested ordering (modelling an indexed read): having no inputs,
/// it passes a required trait set straight through to itself.
/// </summary>
sealed class PhysicalSource : AbstractOp, IPhysicalNode
{

    readonly string _table;

    public PhysicalSource(Cluster cluster, TraitSet traits, string table)
        : base(cluster, traits, ImmutableArray<IOpNode>.Empty)
    {
        _table = table;
    }

    public string Table => _table;

    public override ICost ComputeSelfCost(IPlanner planner) => planner.CostFactory.MakeCost(100, 0);

    public Pair<TraitSet, IList<TraitSet>>? PassThroughTraits(TraitSet required)
    {
        return Pair.Of(required, (IList<TraitSet>)Array.Empty<TraitSet>());
    }

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("table", _table);
        return writer;
    }

    public override IOpNode Copy(TraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new PhysicalSource(Cluster, traits, _table);
    }

}
