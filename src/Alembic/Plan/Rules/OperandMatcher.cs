using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// Matches an <see cref="Operand"/> pattern against a node, descending into the node's children.
/// Lives outside the planner so the planner need not know about operands.
/// </summary>
public static class OperandMatcher
{

    /// <summary>
    /// Whether the operand pattern matches the node and (positionally) its children.
    /// </summary>
    public static bool Matches(Operand operand, INode node)
    {
        return Match(operand, node) is not null;
    }

    /// <summary>
    /// Matches the operand pattern against the node, returning the nodes bound to each operand in
    /// operand order (a pre-order walk, the operand root first), or <c>null</c> if it does not match.
    /// </summary>
    public static ImmutableArray<INode>? Match(Operand operand, INode node)
    {
        var bound = ImmutableArray.CreateBuilder<INode>();
        return MatchInto(operand, node, bound) ? bound.ToImmutable() : null;
    }

    static bool MatchInto(Operand operand, INode node, ImmutableArray<INode>.Builder bound)
    {
        if (!operand.Predicate(node))
            return false;

        bound.Add(node);

        if (operand.Children.IsEmpty)
            return true;

        if (node.Children.Length != operand.Children.Length)
            return false;

        for (int i = 0; i < operand.Children.Length; i++)
        {
            if (!MatchInto(operand.Children[i], node.Children[i], bound))
                return false;
        }

        return true;
    }

}
