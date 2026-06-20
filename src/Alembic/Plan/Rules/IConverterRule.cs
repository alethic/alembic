using Alembic.Algebra;
using Alembic.Plan.Traits;

namespace Alembic.Plan.Rules;

/// <summary>
/// A rule that lowers a node from one convention to another. The author supplies
/// <see cref="In"/>, <see cref="Out"/>, the <see cref="IRule.Operand"/> and <see cref="Convert"/>;
/// the match plumbing is provided here as a mixin rather than a base class.
/// </summary>
public interface IConverterRule : IRule
{

    /// <summary>
    /// The convention this rule converts from.
    /// </summary>
    Convention In { get; }

    /// <summary>
    /// The convention this rule converts to.
    /// </summary>
    Convention Out { get; }

    /// <summary>
    /// Converts a matched node to <see cref="Out"/>, or returns null to decline.
    /// </summary>
    INode? Convert(INode node);

    void IRule.OnMatch(RuleCall call)
    {
        var converted = Convert(call.Node(0));
        if (converted is not null)
            call.Transform(converted);
    }

}
