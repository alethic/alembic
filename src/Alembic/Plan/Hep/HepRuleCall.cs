using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Hep;

/// <summary>
/// The <see cref="RuleCall"/> used by <see cref="HepPlanner"/>. Heuristic rewriting records at most
/// one equivalent per match, which replaces the matched node.
/// </summary>
public sealed class HepRuleCall : RuleCall
{

    /// <summary>
    /// Creates a call over the operand-bound nodes.
    /// </summary>
    public HepRuleCall(ImmutableArray<INode> nodes)
        : base(nodes)
    {

    }

    /// <summary>
    /// The equivalent registered by the rule, if any.
    /// </summary>
    public INode Result { get; private set; } = default!;

    /// <summary>
    /// Whether the rule registered an equivalent.
    /// </summary>
    public bool HasResult { get; private set; }

    /// <inheritdoc />
    public override void Transform(INode equivalent)
    {
        Result = equivalent;
        HasResult = true;
    }

}
