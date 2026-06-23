namespace Alembic.Plan.Rules;

/// <summary>
/// Marks a rule as a transformation rule: one that rewrites a node into an equivalent form within the
/// same convention, rather than implementing it physically. The cost-based planner uses this to keep
/// transformation rules from firing on physical nodes or across convention boundaries.
/// </summary>
[Provenance("org.apache.calcite.rel.rules.TransformationRule")]
public interface ITransformationRule
{
}
