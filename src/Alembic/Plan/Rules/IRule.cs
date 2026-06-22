namespace Alembic.Plan.Rules;

/// <summary>
/// A transformation rule: an <see cref="Operand"/> pattern that selects the nodes it applies to, and
/// the action taken when it matches. The planner matches the operand against each node (via
/// <see cref="OperandMatcher"/>) and calls <see cref="OnMatch"/> for a hit. Every rule has an operand.
/// </summary>
public interface IRule
{

    /// <summary>
    /// The pattern this rule matches.
    /// </summary>
    Operand Operand { get; }

    /// <summary>
    /// Invoked for a matched node; the rule may register an equivalent on the call.
    /// </summary>
    void OnMatch(RuleCall call);

}
