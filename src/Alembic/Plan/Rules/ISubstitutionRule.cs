namespace Alembic.Plan.Rules;

/// <summary>
/// Marks a transformation rule as a <em>substitution</em> rule: the equivalent it produces is meant to
/// supersede the original (e.g. a materialized-view or always-better rewrite). The cost-based planner
/// gives such rules priority in its queue, and — when the rule opts in via <see cref="AutoPruneOld"/> —
/// prunes the original op once the substitute is registered.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.rules.SubstitutionRule")]
public interface ISubstitutionRule : ITransformationRule
{

    /// <summary>
    /// Whether the original op should be pruned automatically once the substitute is registered.
    /// Defaults to <c>false</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.rules.SubstitutionRule", "autoPruneOld()")]
    bool AutoPruneOld => false;

}
