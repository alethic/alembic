using System;
using System.Collections.Immutable;
using System.Linq;

using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// How an operand treats the node's children.
/// </summary>
public enum RuleOperandChildPolicy
{

    /// <summary>
    /// Matches regardless of the node's children (no child operands are checked).
    /// </summary>
    Any,

    /// <summary>
    /// Matches only a node with no children.
    /// </summary>
    Leaf,

    /// <summary>
    /// Matches the child operands against the node's children positionally (same count, in order).
    /// </summary>
    Some,

    /// <summary>
    /// Matches the child operands against the node's children in any order (same count).
    /// </summary>
    Unordered

}

/// <summary>
/// A node-tree pattern. A node matches an operand when it is an instance of <see cref="MatchedClass"/>,
/// carries <see cref="Trait"/> (if one is required), and satisfies the optional <see cref="Predicate"/>;
/// the <see cref="ChildPolicy"/> then governs how the child operands match the node's children. Every
/// <see cref="Rule"/> has one; <see cref="Matches"/> tests a single node, while the recursive tree match
/// against a node's children is performed by the planner as it fires rules.
/// </summary>
public sealed class RuleOperand
{

    static readonly Func<INode, bool> AlwaysMatches = static _ => true;

    /// <summary>
    /// Matches nodes of <paramref name="matchedClass"/>: with child operands they match positionally
    /// (<see cref="RuleOperandChildPolicy.Some"/>); with none it matches only a leaf
    /// (<see cref="RuleOperandChildPolicy.Leaf"/>).
    /// </summary>
    internal RuleOperand(Type matchedClass, params RuleOperand[] children)
        : this(matchedClass, null, AlwaysMatches, children.Length == 0 ? RuleOperandChildPolicy.Leaf : RuleOperandChildPolicy.Some, children)
    {

    }

    /// <summary>
    /// Matches nodes of <paramref name="matchedClass"/> with an explicit child policy.
    /// </summary>
    internal RuleOperand(Type matchedClass, RuleOperandChildPolicy childPolicy, params RuleOperand[] children)
        : this(matchedClass, null, AlwaysMatches, childPolicy, children)
    {

    }

    /// <summary>
    /// Matches nodes of <paramref name="matchedClass"/> that also satisfy <paramref name="predicate"/>.
    /// </summary>
    internal RuleOperand(Type matchedClass, Func<INode, bool> predicate, RuleOperandChildPolicy childPolicy, params RuleOperand[] children)
        : this(matchedClass, null, predicate, childPolicy, children)
    {

    }

    /// <summary>
    /// Matches nodes of <paramref name="matchedClass"/> that also carry <paramref name="trait"/>.
    /// </summary>
    internal RuleOperand(Type matchedClass, ITrait trait, RuleOperandChildPolicy childPolicy, params RuleOperand[] children)
        : this(matchedClass, trait, AlwaysMatches, childPolicy, children)
    {

    }

    /// <summary>
    /// The full form: a matched class, an optional required trait, an extra predicate, a child policy,
    /// and child operands.
    /// </summary>
    internal RuleOperand(Type matchedClass, ITrait? trait, Func<INode, bool> predicate, RuleOperandChildPolicy childPolicy, params RuleOperand[] children)
    {
        MatchedClass = matchedClass;
        Trait = trait;
        Predicate = predicate;
        ChildPolicy = childPolicy;
        Children = children.ToImmutableArray();
    }

    /// <summary>
    /// The rule this operand belongs to. Assigned when the rule flattens its operand tree.
    /// </summary>
    public Rule Rule { get; internal set; } = null!;

    /// <summary>
    /// The operand directly above this one, or <c>null</c> for the root operand. Assigned during
    /// flattening.
    /// </summary>
    public RuleOperand? Parent { get; internal set; }

    /// <summary>
    /// This operand's position among its parent's child operands (0 for the root). Assigned during
    /// flattening.
    /// </summary>
    public int OrdinalInParent { get; internal set; }

    /// <summary>
    /// This operand's position in the rule's flattened operand list. Assigned during flattening.
    /// </summary>
    public int OrdinalInRule { get; internal set; }

    /// <summary>
    /// The order in which to solve operands when matching is seeded from this operand: itself, then its
    /// parents up to the root, then the remaining operands in prefix order. Assigned during flattening.
    /// </summary>
    public int[] SolveOrder { get; internal set; } = Array.Empty<int>();

    /// <summary>
    /// The node type this operand matches.
    /// </summary>
    public Type MatchedClass { get; }

    /// <summary>
    /// A trait the node must carry, or <c>null</c> if the operand does not test traits.
    /// </summary>
    public ITrait? Trait { get; }

    /// <summary>
    /// An extra condition applied after the class (and trait) test; defaults to always true.
    /// </summary>
    public Func<INode, bool> Predicate { get; }

    /// <summary>
    /// How this operand treats the node's children.
    /// </summary>
    public RuleOperandChildPolicy ChildPolicy { get; }

    /// <summary>
    /// The child operands (empty for <see cref="RuleOperandChildPolicy.Any"/> and
    /// <see cref="RuleOperandChildPolicy.Leaf"/>).
    /// </summary>
    public ImmutableArray<RuleOperand> Children { get; }

    /// <summary>
    /// Whether <paramref name="node"/> matches this operand's class, trait, and predicate (the children
    /// are matched separately, by the matcher, per the <see cref="ChildPolicy"/>).
    /// </summary>
    public bool Matches(INode node)
    {
        if (!MatchedClass.IsInstanceOfType(node))
            return false;

        if (Trait is not null && !node.Traits.Contains(Trait))
            return false;

        return Predicate(node);
    }

    /// <summary>
    /// Two operands are equal when they match the same class, require the same trait, and have equal
    /// child operands. The predicate and child policy are not part of the identity (matching them is the
    /// rule's job, not the operand's).
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        return obj is RuleOperand that
            && MatchedClass == that.MatchedClass
            && Equals(Trait, that.Trait)
            && Children.SequenceEqual(that.Children);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MatchedClass);
        hash.Add(Trait);
        foreach (var child in Children)
            hash.Add(child);

        return hash.ToHashCode();
    }

}
