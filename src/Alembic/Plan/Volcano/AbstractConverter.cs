using System.Collections.Immutable;
using System.Diagnostics;

using Alembic.Algebra;
using Alembic.Algebra.Convert;

namespace Alembic.Plan.Volcano;

/// <summary>
/// A placeholder that demands its input be delivered with a different trait set. It is always
/// unimplementable on its own — its cost is infinite — so the planner must replace it, via
/// <see cref="ExpandConversionRule"/>, with a real chain of converters. This is how a cost-based
/// planner reaches a required output convention.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.AbstractConverter")]
public class AbstractConverter : ConverterImpl
{

    /// <summary>
    /// Creates a converter that requires <paramref name="input"/> in the given target traits.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.AbstractConverter", "AbstractConverter(RelOptCluster, RelSubset, RelTraitDef, RelTraitSet)")]
    public AbstractConverter(OpTraitSet target, IOp input, OpTraitDef? traitDef = null)
        : base(traitDef, target, input)
    {
        Debug.Assert(target.AllSimple());
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.AbstractConverter", "explainTerms(RelWriter)")]
    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        foreach (var trait in Traits)
            writer.Item(trait.TraitDef.Name, trait);

        return writer;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.AbstractConverter", "computeSelfCost(RelOptPlanner, RelMetadataQuery)")]
    public override IOpCost ComputeSelfCost(IOpPlanner planner) => planner.CostFactory.MakeInfiniteCost();

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.AbstractConverter", "isEnforcer()")]
    public bool IsEnforcer => true;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.AbstractConverter", "copy(RelTraitSet, List<RelNode>)")]
    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new AbstractConverter(traits, children[0], TraitDef);
    }

}
