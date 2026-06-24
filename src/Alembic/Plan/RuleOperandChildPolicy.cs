namespace Alembic.Plan;

/// <summary>
/// How an operand treats the op's children.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperandChildPolicy")]
public enum RuleOperandChildPolicy
{

    /// <summary>
    /// Matches regardless of the op's children (no child operands are checked).
    /// </summary>
    Any,

    /// <summary>
    /// Matches only an op with no children.
    /// </summary>
    Leaf,

    /// <summary>
    /// Matches the child operands against the op's children positionally (same count, in order).
    /// </summary>
    Some,

    /// <summary>
    /// Each child operand matches any one of the op's children, regardless of position; the op's
    /// child count is not constrained.
    /// </summary>
    Unordered

}
