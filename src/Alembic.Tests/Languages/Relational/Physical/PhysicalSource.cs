using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Algebra.Metadata;
using Alembic.Plan;
using Alembic.Util;

namespace Alembic.Tests.Languages.Relational.Physical;

/// <summary>
/// The physical counterpart of a logical source. Scanning the source has a fixed cost. As a physical
/// op it can natively deliver any requested ordering (modelling an indexed read): having no inputs,
/// it passes a required trait set straight through to itself.
/// </summary>
sealed class PhysicalSource : AbstractOp, IPhysicalOp
{

    readonly string _table;

    public PhysicalSource(OpCluster cluster, OpTraitSet traits, string table)
        : base(cluster, traits)
    {
        _table = table;
    }

    public string Table => _table;

    public override IOpCost ComputeSelfCost(IOpPlanner planner, OpMetadataQuery mq) => planner.CostFactory.MakeCost(100, 0);

    public Pair<OpTraitSet, IList<OpTraitSet>>? PassThroughTraits(OpTraitSet required)
    {
        return Pair.Of(required, (IList<OpTraitSet>)Array.Empty<OpTraitSet>());
    }

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("table", _table);
        return writer;
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new PhysicalSource(Cluster, traits, _table);
    }

}
