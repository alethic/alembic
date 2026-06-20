namespace Alembic.Plan.Rules;

/// <summary>
/// A transformation rule: an <see cref="Operand"/> pattern plus the action taken when it matches.
/// </summary>
public interface IRule
{

    /// <summary>
    /// The pattern this rule matches.
    /// </summary>
    Operand Operand { get; }

    /// <summary>
    /// Invoked for a successful match; the rule may register an equivalent on the call.
    /// </summary>
    void OnMatch(RuleCall call);

    /// <summary>
    /// An optional guard run before <see cref="OnMatch"/>. Defaults to always matching.
    /// </summary>
    bool Matches(RuleCall call)
    {
        return true;
    }

}
