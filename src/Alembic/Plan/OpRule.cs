using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

using Alembic.Algebra;
using Alembic.Algebra.Convert;

namespace Alembic.Plan;

/// <summary>
/// A transformation rule: a root <see cref="OpRuleOperand"/> selecting the ops it applies to, and the
/// action taken on a match (<see cref="OnMatch"/>). A planner matches the operand against each op and,
/// if <see cref="Matches"/> also allows it, calls <see cref="OnMatch"/>.
/// </summary>
/// <remarks>
/// Operands form a closed world: a rule builds its operand only through the static factory methods here —
/// the <see cref="Operand{TOp}"/> / <see cref="OperandJ{TOp}"/> builders combined with the
/// <see cref="Some"/>, <see cref="Unordered"/>, <see cref="None"/>, and <see cref="Any"/> child-operand
/// lists (plus <see cref="ConvertOperand{TOp}"/> for converter rules), passing the result to the
/// constructor. The <see cref="OpRuleOperand"/> constructor itself is not public, so only well-formed
/// operand trees can exist.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule")]
public abstract class OpRule
{

    /// <summary>
    /// Initializes the rule with its root operand (built from the factory methods below).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "RelOptRule(RelOptRuleOperand)")]
    protected OpRule(OpRuleOperand operand)
    {
        _operand = operand;
        Operands = FlattenOperands(operand);
        AssignSolveOrder(Operands);
    }

    readonly OpRuleOperand _operand;

    /// <summary>
    /// The pattern this rule matches. (A method, not a property: Calcite's static factory
    /// <see cref="Operand{TOp}"/> mirrors <c>operand(...)</c> and the accessor mirrors <c>getOperand()</c>
    /// — both distinct in Java, but a property and same-named static method cannot coexist in C#.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "getOperand()")]
    public OpRuleOperand GetOperand() => _operand;

    /// <summary>
    /// The rule's operands flattened into a single list in prefix order (root first), each tagged with
    /// its position. A planner indexes these by <see cref="OpRuleOperand.MatchedType"/> so that an op
    /// can seed a match at any operand position.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "getOperands()")]
    public ImmutableArray<OpRuleOperand> Operands { get; }

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
    public virtual bool Matches(OpRuleCall call)
    {
        return true;
    }

    /// <summary>
    /// The convention of the result of firing this rule, null if not known.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "getOutConvention()")]
    public virtual IConvention? OutConvention => null;

    /// <summary>
    /// The trait of the result of firing this rule, null if not known.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "getOutTrait()")]
    public virtual IOpTrait? OutTrait => null;

    /// <summary>
    /// Invoked for a matched op; the rule registers equivalents on the call.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "onMatch(RelOptRuleCall)")]
    public abstract void OnMatch(OpRuleCall call);

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
    /// A rule is identified by its <see cref="Description"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "toString()")]
    public sealed override string ToString()
    {
        return Description;
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
            || (obj is OpRule that
                && GetType() == that.GetType()
                && Description == that.Description
                && GetOperand().Equals(that.GetOperand()));
    }

    // ~ Operand factories — Calcite's two-layer operand construction: the operand/operandJ builders plus
    // the some/none/any/unordered child-operand lists (all @Deprecated // to be removed before 2.0 in
    // Calcite, superseded by RelRule.Config, which Alembic does not port). ConvertOperand mirrors
    // convertOperand; its converter-on-converter guard lives on the nested ConverterOpRuleOperand.

    /// <summary>
    /// Creates an operand matching an op of type <typeparamref name="TOp"/> with the given child operands.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "operand(Class, RelOptRuleOperandChildren)")]
    public static OpRuleOperand Operand<TOp>(OpRuleOperandChildren operandList)
        where TOp : IOp
    {
        return new OpRuleOperand(typeof(TOp), null, static _ => true, operandList.Policy, operandList.Operands);
    }

    /// <summary>
    /// Creates an operand matching an op of type <typeparamref name="TOp"/> that carries
    /// <paramref name="trait"/> and satisfies <paramref name="predicate"/>, with the given child operands.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "operandJ(Class, RelTrait, Predicate, RelOptRuleOperandChildren)")]
    public static OpRuleOperand OperandJ<TOp>(IOpTrait? trait, Func<IOp, bool> predicate, OpRuleOperandChildren operandList)
        where TOp : IOp
    {
        return new OpRuleOperand(typeof(TOp), trait, predicate, operandList.Policy, operandList.Operands);
    }

    /// <summary>
    /// A list of child operands that matches child ops in the order they appear.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "some(RelOptRuleOperand, RelOptRuleOperand...)")]
    public static OpRuleOperandChildren Some(OpRuleOperand first, params OpRuleOperand[] rest)
    {
        return new OpRuleOperandChildren(RuleOperandChildPolicy.Some, AsList(first, rest));
    }

    /// <summary>
    /// A list of child operands that matches child ops in any order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "unordered(RelOptRuleOperand, RelOptRuleOperand...)")]
    public static OpRuleOperandChildren Unordered(OpRuleOperand first, params OpRuleOperand[] rest)
    {
        return new OpRuleOperandChildren(RuleOperandChildPolicy.Unordered, AsList(first, rest));
    }

    /// <summary>
    /// An empty list of child operands (matches a leaf op, one with no children).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "none()")]
    public static OpRuleOperandChildren None()
    {
        return OpRuleOperandChildren.LeafChildren;
    }

    /// <summary>
    /// A list of child operands that matches any number of child ops.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "any()")]
    public static OpRuleOperandChildren Any()
    {
        return OpRuleOperandChildren.AnyChildren;
    }

    // The .NET stand-in for Guava's Lists.asList(first, rest): a list whose head is first, tail is rest.
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Lists", "asList(E, E[])")]
    static OpRuleOperand[] AsList(OpRuleOperand first, OpRuleOperand[] rest)
    {
        var operands = new OpRuleOperand[rest.Length + 1];
        operands[0] = first;
        System.Array.Copy(rest, 0, operands, 1, rest.Length);
        return operands;
    }

    /// <summary>
    /// An operand matching an op of type <typeparamref name="TOp"/> that carries <paramref name="trait"/>
    /// — used to match an op needing conversion.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "convertOperand(Class, Predicate, RelTrait)")]
    protected static ConverterOpRuleOperand ConvertOperand<TOp>(Func<IOp, bool> predicate, IOpTrait trait)
        where TOp : IOp
    {
        return new ConverterOpRuleOperand(typeof(TOp), trait, predicate);
    }

    /// <summary>
    /// Converts <paramref name="rel"/> so it carries <paramref name="toTrait"/> (e.g. a target
    /// convention), returning <paramref name="rel"/> unchanged when it already matches. This is how a rule
    /// asks the planner for an input in a particular trait before building an op over it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "convert(RelNode, RelTrait)")]
    public static IOp Convert(IOp rel, IOpTrait? toTrait)
    {
        return Convert(rel.Cluster.Planner, rel, toTrait);
    }

    /// <summary>
    /// Converts <paramref name="rel"/> so that its trait set includes <paramref name="toTrait"/>, asking
    /// <paramref name="planner"/> to change traits if it does not already match.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "convert(RelOptPlanner, RelNode, RelTrait)")]
    public static IOp Convert(IOpPlanner planner, IOp rel, IOpTrait? toTrait)
    {
        var outTraits = rel.Traits;
        if (toTrait is not null)
            outTraits = outTraits.Replace(toTrait);

        if (rel.Traits.Matches(outTraits))
            return rel;

        return planner.ChangeTraits(rel, outTraits.Simplify());
    }

    // ~ Operand flattening -----------------------------------------------------

    /// <summary>
    /// Flattens the operand tree into a list in prefix order, wiring each operand's rule, parent, and
    /// ordinals as it goes.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule", "flattenOperands(RelOptRuleOperand)")]
    ImmutableArray<OpRuleOperand> FlattenOperands(OpRuleOperand rootOperand)
    {
        var operandList = new List<OpRuleOperand>();

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
    void FlattenRecurse(List<OpRuleOperand> operandList, OpRuleOperand parentOperand)
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
    static void AssignSolveOrder(IReadOnlyList<OpRuleOperand> operands)
    {
        foreach (var operand in operands)
        {
            operand.SolveOrder = new int[operands.Count];
            int m = 0;
            for (OpRuleOperand? o = operand; o is not null; o = o.Parent)
                operand.SolveOrder[m++] = o.OrdinalInRule;

            for (int k = 0; k < operands.Count; k++)
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

            // Assert: operand appears once in the sort-order.
            Debug.Assert(m == operands.Count);
        }
    }

    /// <summary>
    /// Operand to an instance of the converter rule.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule.ConverterRelOptRuleOperand")]
    protected class ConverterOpRuleOperand : OpRuleOperand
    {

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule.ConverterRelOptRuleOperand", "ConverterRelOptRuleOperand(Class, RelTrait, Predicate)")]
        internal ConverterOpRuleOperand(Type clazz, IOpTrait? inTrait, Func<IOp, bool> predicate)
            : base(clazz, inTrait, predicate, RuleOperandChildPolicy.Any, ImmutableArray<OpRuleOperand>.Empty)
        {
        }

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptRule.ConverterRelOptRuleOperand", "matches(RelNode)")]
        public override bool Matches(IOp op)
        {
            // Don't apply converters to converters that operate
            // on the same trait dimension -- otherwise we get
            // an n^2 effect.
            if (op is IConverter converter)
            {
                if (ReferenceEquals(((ConverterRule)Rule).TraitDef, converter.TraitDef))
                {
                    return false;
                }
            }

            return base.Matches(op);
        }
    }

}
