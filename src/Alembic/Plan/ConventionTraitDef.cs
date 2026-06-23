using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Alembic.Algebra;
using Alembic.Plan.Rules;
using Alembic.Plan.Volcano;
using Alembic.Util;
using Alembic.Util.Graph;

namespace Alembic.Plan;

/// <summary>
/// The trait dimension for <see cref="IConvention"/>. Per planner it holds a graph of conventions, wired
/// by the <em>guaranteed</em> converter rules registered with it; converting an op between conventions
/// walks a shortest path through that graph, applying the converter rules along each arc.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef")]
public class ConventionTraitDef : OpTraitDef<IConvention>
{

    /// <summary>
    /// The singleton instance.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef", "INSTANCE")]
    public static readonly ConventionTraitDef Instance = new ConventionTraitDef();

    // Per-planner conversion data, reclaimed once the planner is collected — the analog of Calcite's
    // weak-keyed LoadingCache<RelOptPlanner, ConversionData>.
    readonly ConditionalWeakTable<IOpPlanner, ConversionData> _conversionCache = new ConditionalWeakTable<IOpPlanner, ConversionData>();

    ConventionTraitDef()
    {

    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef", "getSimpleName()")]
    public override string Name => "convention";

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef", "getDefault()")]
    public override IConvention Default => Convention.None;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef", "registerConverterRule(RelOptPlanner, ConverterRule)")]
    public override void RegisterConverterRule(IOpPlanner planner, ConverterRule converterRule)
    {
        if (!converterRule.IsGuaranteed)
            return;

        var conversionData = GetConversionData(planner);
        var inConvention = (IConvention)converterRule.Source;
        var outConvention = (IConvention)converterRule.Target;
        conversionData.ConversionGraph.AddVertex(inConvention);
        conversionData.ConversionGraph.AddVertex(outConvention);
        conversionData.ConversionGraph.AddEdge(inConvention, outConvention);
        conversionData.MapArcToConverterRule.Put(Pair.Of(inConvention, outConvention), converterRule);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef", "deregisterConverterRule(RelOptPlanner, ConverterRule)")]
    public override void DeregisterConverterRule(IOpPlanner planner, ConverterRule converterRule)
    {
        if (!converterRule.IsGuaranteed)
            return;

        var conversionData = GetConversionData(planner);
        var inConvention = (IConvention)converterRule.Source;
        var outConvention = (IConvention)converterRule.Target;
        conversionData.ConversionGraph.RemoveEdge(inConvention, outConvention);
        conversionData.MapArcToConverterRule.Remove(Pair.Of(inConvention, outConvention), converterRule);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef", "convert(RelOptPlanner, RelNode, Convention, boolean)")]
    public override IOp? Convert(IOpPlanner planner, IOp rel, IOpTrait toTrait, bool allowInfiniteCostConverters)
    {
        var toConvention = (IConvention)toTrait;
        var conversionData = GetConversionData(planner);
        var fromConvention = rel.Convention;

        var conversionPaths = conversionData.GetPaths(fromConvention, toConvention);

        foreach (var conversionPath in conversionPaths)
        {
            var converted = rel;
            IConvention? previous = null;
            var failed = false;
            foreach (var arc in conversionPath)
            {
                var cost = ((VolcanoPlanner)planner).GetCost(converted);
                if ((cost is null || cost.IsInfinite) && !allowInfiniteCostConverters)
                {
                    failed = true;
                    break;
                }

                if (previous is not null)
                {
                    var changed = ChangeConvention(converted, previous, arc, conversionData);
                    if (changed is null)
                        throw new System.InvalidOperationException($"Converter from {previous} to {arc} guaranteed that it could convert any op.");

                    converted = changed;
                }

                previous = arc;
            }

            if (!failed)
                return converted;
        }

        return null;
    }

    /// <summary>
    /// Tries to convert <paramref name="rel"/> to <paramref name="target"/> by applying each converter
    /// rule registered for the <paramref name="source"/>→<paramref name="target"/> arc, in turn.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef", "changeConvention(RelNode, Convention, Convention, Multimap)")]
    static IOp? ChangeConvention(IOp rel, IConvention source, IConvention target, ConversionData conversionData)
    {
        foreach (var rule in conversionData.MapArcToConverterRule.Get(Pair.Of(source, target)))
        {
            var converted = rule.Convert(rel);
            if (converted is not null)
                return converted;
        }

        return null;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef", "canConvert(RelOptPlanner, Convention, Convention)")]
    public override bool CanConvert(IOpPlanner planner, IOpTrait fromTrait, IOpTrait toTrait)
    {
        var fromConvention = (IConvention)fromTrait;
        var toConvention = (IConvention)toTrait;
        var conversionData = GetConversionData(planner);
        return fromConvention.CanConvertConvention(toConvention)
            || conversionData.GetShortestDistance(fromConvention, toConvention) != -1;
    }

    ConversionData GetConversionData(IOpPlanner planner) => _conversionCache.GetValue(planner, _ => new ConversionData());

    /// <summary>
    /// Per-planner workspace for converting from one convention to another: the conversion graph and the
    /// converter rules on each arc.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef.ConversionData")]
    sealed class ConversionData
    {

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef.ConversionData", "conversionGraph")]
        internal readonly DefaultDirectedGraph<IConvention, DefaultEdge> ConversionGraph = DefaultDirectedGraph<IConvention, DefaultEdge>.Create();

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef.ConversionData", "mapArcToConverterRule")]
        internal readonly Multimap<Pair<IConvention, IConvention>, ConverterRule> MapArcToConverterRule = new Multimap<Pair<IConvention, IConvention>, ConverterRule>();

        Graphs.FrozenGraph<IConvention, DefaultEdge>? _pathMap;

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef.ConversionData", "getPaths(Convention, Convention)")]
        public List<List<IConvention>> GetPaths(IConvention from, IConvention to) => GetPathMap().GetPaths(from, to);

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef.ConversionData", "getShortestDistance(Convention, Convention)")]
        public int GetShortestDistance(IConvention from, IConvention to) => GetPathMap().GetShortestDistance(from, to);

        // As in Calcite, the path map is frozen once (on first query); converter rules are expected to be
        // registered before planning begins.
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.ConventionTraitDef.ConversionData", "getPathMap()")]
        Graphs.FrozenGraph<IConvention, DefaultEdge> GetPathMap() => _pathMap ??= Graphs.MakeImmutable(ConversionGraph);

    }

}
