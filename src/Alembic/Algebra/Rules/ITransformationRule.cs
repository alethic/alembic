namespace Alembic.Algebra.Rules;

/// <summary>
/// Marks a rule as a transformation rule: one that rewrites an op into an equivalent form within the
/// same convention, rather than implementing it physically. The cost-based planner uses this to keep
/// transformation rules from firing on physical ops or across convention boundaries.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.rules.TransformationRule")]
public interface ITransformationRule
{
}
