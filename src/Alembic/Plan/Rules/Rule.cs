using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// A transformation rule: a root <see cref="RuleOperand"/> selecting the ops it applies to, and the
/// action taken on a match (<see cref="OnMatch"/>). A planner matches the operand against each op and,
/// if <see cref="Matches"/> also allows it, calls <see cref="OnMatch"/>.
/// </summary>
/// <remarks>
/// Operands form a closed world: a rule builds its operand only through the <c>protected static</c>
/// factory methods here (<see cref="Any{TOp}()"/>, <see cref="Leaf{TOp}"/>, <see cref="Some{TOp}"/>,
/// <see cref="Unordered{TOp}"/>, <see cref="ConvertOperand{TOp}"/>), passing the result to the
/// constructor. The <see cref="RuleOperand"/> constructor itself is not public, so only well-formed
/// operand trees can exist.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule")]
public abstract class Rule
{

    /// <summary>
    /// Initializes the rule with its root operand (built from the factory methods below).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "RelOptRule(RelOptRuleOperand)")]
    protected Rule(RuleOperand operand)
    {
        Operand = operand;
        Operands = FlattenOperands(operand);
        AssignSolveOrder(Operands);
    }

    /// <summary>
    /// The pattern this rule matches.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "getOperand()")]
    public RuleOperand Operand { get; }

    /// <summary>
    /// The rule's operands flattened into a single list in prefix order (root first), each tagged with
    /// its position. A planner indexes these by <see cref="RuleOperand.MatchedClass"/> so that an op
    /// can seed a match at any operand position.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "getOperands()")]
    public ImmutableArray<RuleOperand> Operands { get; }

    /// <summary>
    /// A description identifying this rule (used to look it up from a program instruction). Defaults to
    /// the rule's type name.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "toString()")]
    public virtual string Description => GetType().Name;

    /// <summary>
    /// A side-condition checked after the operand matches and before <see cref="OnMatch"/>. The default
    /// always allows the match.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "matches(RelOptRuleCall)")]
    public virtual bool Matches(RuleCall call)
    {
        return true;
    }

    /// <summary>
    /// Invoked for a matched op; the rule registers equivalents on the call.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "onMatch(RelOptRuleCall)")]
    public abstract void OnMatch(RuleCall call);

    /// <summary>
    /// Identifies the rule by its <see cref="Description"/> alone: a planner requires every registered
    /// rule to have a unique description, so the description is a sufficient hash.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "hashCode()")]
    public override int GetHashCode()
    {
        return Description.GetHashCode();
    }

    /// <summary>
    /// Two rules are equal when they are the same type, carry the same <see cref="Description"/>, and have
    /// equal root operands. The class and operand are included so that a poorly chosen (colliding)
    /// description does not make distinct rules compare equal.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "equals(Object)")]
    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj)
            || (obj is Rule that
                && GetType() == that.GetType()
                && Description == that.Description
                && Operand.Equals(that.Operand));
    }

    // ~ Operand factories (the RelOptRule operand/some/any/none/unordered/convertOperand analogs) ------

    /// <summary>
    /// An operand matching an op of type <typeparamref name="TOp"/> regardless of its children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "any()")]
    protected static RuleOperand Any<TOp>()
        where TOp : IOpNode
    {
        return new RuleOperand(typeof(TOp), RuleOperandChildPolicy.Any);
    }

    /// <summary>
    /// An operand matching an op of type <typeparamref name="TOp"/> that also satisfies a predicate,
    /// regardless of its children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "any()")]
    protected static RuleOperand Any<TOp>(Func<IOpNode, bool> predicate)
        where TOp : IOpNode
    {
        return new RuleOperand(typeof(TOp), predicate, RuleOperandChildPolicy.Any);
    }

    /// <summary>
    /// An operand matching a leaf op of type <typeparamref name="TOp"/> (one with no children).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "none()")]
    protected static RuleOperand Leaf<TOp>()
        where TOp : IOpNode
    {
        return new RuleOperand(typeof(TOp), RuleOperandChildPolicy.Leaf);
    }

    /// <summary>
    /// An operand matching an op of type <typeparamref name="TOp"/> whose children match the given
    /// child operands positionally.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "some(RelOptRuleOperand, RelOptRuleOperand...)")]
    protected static RuleOperand Some<TOp>(params RuleOperand[] children)
        where TOp : IOpNode
    {
        return new RuleOperand(typeof(TOp), RuleOperandChildPolicy.Some, children);
    }

    /// <summary>
    /// An operand matching an op of type <typeparamref name="TOp"/> whose children match the given
    /// child operands in any order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "unordered(RelOptRuleOperand, RelOptRuleOperand...)")]
    protected static RuleOperand Unordered<TOp>(params RuleOperand[] children)
        where TOp : IOpNode
    {
        return new RuleOperand(typeof(TOp), RuleOperandChildPolicy.Unordered, children);
    }

    /// <summary>
    /// An operand matching an op of type <typeparamref name="TOp"/> that carries <paramref name="trait"/>
    /// — used to match an op needing conversion.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "convertOperand(Class, Predicate, RelTrait)")]
    protected static RuleOperand ConvertOperand<TOp>(ITrait trait)
        where TOp : IOpNode
    {
        return new RuleOperand(typeof(TOp), trait, RuleOperandChildPolicy.Any);
    }

    // ~ Operand flattening -----------------------------------------------------

    /// <summary>
    /// Flattens the operand tree into a list in prefix order, wiring each operand's rule, parent, and
    /// ordinals as it goes.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "flattenOperands(RelOptRuleOperand)")]
    ImmutableArray<RuleOperand> FlattenOperands(RuleOperand rootOperand)
    {
        var operandList = new List<RuleOperand>();

        rootOperand.Rule = this;
        rootOperand.Parent = null;
        rootOperand.OrdinalInParent = 0;
        rootOperand.OrdinalInRule = operandList.Count;
        operandList.Add(rootOperand);
        FlattenRecurse(operandList, rootOperand);
        return operandList.ToImmutableArray();
    }

    /// <summary>
    /// Adds the operand's descendants to the list in prefix order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "flattenRecurse(List<RelOptRuleOperand>, RelOptRuleOperand)")]
    void FlattenRecurse(List<RuleOperand> operandList, RuleOperand parentOperand)
    {
        int k = 0;
        foreach (var operand in parentOperand.Children)
        {
            operand.Rule = this;
            operand.Parent = parentOperand;
            operand.OrdinalInParent = k++;
            operand.OrdinalInRule = operandList.Count;
            operandList.Add(operand);
            FlattenRecurse(operandList, operand);
        }
    }

    /// <summary>
    /// Builds each operand's solve order: itself, then its parents up to the root, then the remaining
    /// operands in prefix order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "assignSolveOrder(List<RelOptRuleOperand>)")]
    static void AssignSolveOrder(ImmutableArray<RuleOperand> operands)
    {
        foreach (var operand in operands)
        {
            operand.SolveOrder = new int[operands.Length];
            int m = 0;
            for (RuleOperand? o = operand; o is not null; o = o.Parent)
                operand.SolveOrder[m++] = o.OrdinalInRule;

            for (int k = 0; k < operands.Length; k++)
            {
                bool exists = false;
                for (int n = 0; n < m; n++)
                {
                    if (operand.SolveOrder[n] == k)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    operand.SolveOrder[m++] = k;
            }
        }
    }

}
