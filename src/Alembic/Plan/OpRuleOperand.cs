using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using Alembic.Algebra;
using Alembic.Algebra.Convert;

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

/// <summary>
/// An op-tree pattern. An op matches an operand when it is an instance of <see cref="MatchedType"/>,
/// carries <see cref="Trait"/> (if one is required), and satisfies the optional <see cref="Predicate"/>;
/// the <see cref="ChildPolicy"/> then governs how the child operands match the op's children. Every
/// <see cref="Rule"/> has one; <see cref="Matches"/> tests a single op, while the recursive tree match
/// against an op's children is performed by the planner as it fires rules.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand")]
public sealed class OpRuleOperand
{

    /// <summary>
    /// Creates an operand matching <paramref name="matchedClass"/> that carries <paramref name="trait"/>
    /// (or any trait, when null) and satisfies <paramref name="predicate"/>, with
    /// <paramref name="childPolicy"/> governing how <paramref name="children"/> match the op's children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "RelOptRuleOperand(Class, RelTrait, Predicate, RelOptRuleOperandChildPolicy, ImmutableList<RelOptRuleOperand>)")]
    internal OpRuleOperand(Type matchedClass, IOpTrait? trait, Func<IOp, bool> predicate, RuleOperandChildPolicy childPolicy, ImmutableArray<OpRuleOperand> children)
    {
        MatchedType = matchedClass;
        switch (childPolicy)
        {
            case RuleOperandChildPolicy.Any:
                break;
            case RuleOperandChildPolicy.Leaf:
                if (!children.IsEmpty)
                    throw new ArgumentException();
                break;
            case RuleOperandChildPolicy.Unordered:
                Debug.Assert(children.Length == 1);
                break;
            default:
                if (children.IsEmpty)
                    throw new ArgumentException();
                break;
        }

        ChildPolicy = childPolicy;
        Trait = trait;
        Predicate = predicate;
        Children = children;
        foreach (var child in Children)
        {
            Debug.Assert(child.Parent is null, "cannot re-use operands");
            child.Parent = this;
        }
    }

    /// <summary>
    /// The rule this operand belongs to. Assigned when the rule flattens its operand tree.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "getRule()")]
    public OpRule Rule { get; internal set; } = null!;

    /// <summary>
    /// The operand directly above this one, or <c>null</c> for the root operand. Assigned during
    /// flattening.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "getParent()")]
    public OpRuleOperand? Parent { get; internal set; }

    /// <summary>
    /// This operand's position among its parent's child operands (0 for the root). Assigned during
    /// flattening.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "ordinalInParent")]
    public int OrdinalInParent;

    /// <summary>
    /// This operand's position in the rule's flattened operand list. Assigned during flattening.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "ordinalInRule")]
    public int OrdinalInRule;

    /// <summary>
    /// The order in which to solve operands when matching is seeded from this operand: itself, then its
    /// parents up to the root, then the remaining operands in prefix order. Assigned during flattening.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "solveOrder")]
    public int[] SolveOrder = Array.Empty<int>();

    /// <summary>
    /// The op type this operand matches.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "getMatchedClass()")]
    public Type MatchedType { get; }

    /// <summary>
    /// A trait the op must carry, or <c>null</c> if the operand does not test traits.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "trait")]
    public readonly IOpTrait? Trait;

    /// <summary>
    /// An extra condition applied after the class (and trait) test; defaults to always true.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "predicate")]
    internal readonly Func<IOp, bool> Predicate;

    /// <summary>
    /// Whether this operand carries the converter-on-converter guard. Set by the converter operand factory,
    /// it applies the same check as Calcite's <c>ConverterRelOptRuleOperand.matches</c> so a converter rule
    /// does not match its own output (avoiding an n² effect).
    /// </summary>
    internal bool IsConverterOperand { get; init; }

    /// <summary>
    /// How this operand treats the op's children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "childPolicy")]
    public readonly RuleOperandChildPolicy ChildPolicy;

    /// <summary>
    /// The child operands (empty for <see cref="RuleOperandChildPolicy.Any"/> and
    /// <see cref="RuleOperandChildPolicy.Leaf"/>).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "getChildOperands()")]
    public ImmutableArray<OpRuleOperand> Children { get; }

    /// <summary>
    /// Whether <paramref name="op"/> matches this operand's class, trait, and predicate (the children
    /// are matched separately, by the matcher, per the <see cref="ChildPolicy"/>).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "matches(RelNode)")]
    public bool Matches(IOp op)
    {
        // Don't apply a converter rule to a converter that operates on the same trait dimension —
        // otherwise we get an n^2 effect (Calcite's ConverterRelOptRuleOperand.matches).
        if (IsConverterOperand && op is IConverter converter && Rule is ConverterRule rule
            && ReferenceEquals(rule.TraitDef, converter.TraitDef))
            return false;

        if (!MatchedType.IsInstanceOfType(op))
            return false;

        if (Trait is not null && !op.Traits.Contains(Trait))
            return false;

        return Predicate(op);
    }

    /// <summary>
    /// Two operands are equal when they match the same class, require the same trait, and have equal
    /// child operands. The predicate and child policy are not part of the identity (matching them is the
    /// rule's job, not the operand's).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "equals(Object)")]
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        return obj is OpRuleOperand that
            && MatchedType == that.MatchedType
            && Equals(Trait, that.Trait)
            && Children.SequenceEqual(that.Children);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRuleOperand", "hashCode()")]
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MatchedType);
        hash.Add(Trait);
        foreach (var child in Children)
            hash.Add(child);

        return hash.ToHashCode();
    }

}
