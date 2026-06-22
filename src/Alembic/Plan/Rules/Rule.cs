using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// A transformation rule: a root <see cref="RuleOperand"/> selecting the nodes it applies to, and the
/// action taken on a match (<see cref="OnMatch"/>). A planner matches the operand against each node and,
/// if <see cref="Matches"/> also allows it, calls <see cref="OnMatch"/>.
/// </summary>
/// <remarks>
/// Operands form a closed world: a rule builds its operand only through the <c>protected static</c>
/// factory methods here (<see cref="Any{TNode}()"/>, <see cref="Leaf{TNode}"/>, <see cref="Some{TNode}"/>,
/// <see cref="Unordered{TNode}"/>, <see cref="ConvertOperand{TNode}"/>), passing the result to the
/// constructor. The <see cref="RuleOperand"/> constructor itself is not public, so only well-formed
/// operand trees can exist.
/// </remarks>
public abstract class Rule
{

    /// <summary>
    /// Initializes the rule with its root operand (built from the factory methods below).
    /// </summary>
    protected Rule(RuleOperand operand)
    {
        Operand = operand;
        Operands = FlattenOperands(operand);
        AssignSolveOrder(Operands);
    }

    /// <summary>
    /// The pattern this rule matches.
    /// </summary>
    public RuleOperand Operand { get; }

    /// <summary>
    /// The rule's operands flattened into a single list in prefix order (root first), each tagged with
    /// its position. A planner indexes these by <see cref="RuleOperand.MatchedClass"/> so that a node
    /// can seed a match at any operand position.
    /// </summary>
    public ImmutableArray<RuleOperand> Operands { get; }

    /// <summary>
    /// A description identifying this rule (used to look it up from a program instruction). Defaults to
    /// the rule's type name.
    /// </summary>
    public virtual string Description => GetType().Name;

    /// <summary>
    /// A side-condition checked after the operand matches and before <see cref="OnMatch"/>. The default
    /// always allows the match.
    /// </summary>
    public virtual bool Matches(RuleCall call)
    {
        return true;
    }

    /// <summary>
    /// Invoked for a matched node; the rule registers equivalents on the call.
    /// </summary>
    public abstract void OnMatch(RuleCall call);

    /// <summary>
    /// Identifies the rule by its <see cref="Description"/> alone: a planner requires every registered
    /// rule to have a unique description, so the description is a sufficient hash.
    /// </summary>
    public override int GetHashCode()
    {
        return Description.GetHashCode();
    }

    /// <summary>
    /// Two rules are equal when they are the same type, carry the same <see cref="Description"/>, and have
    /// equal root operands. The class and operand are included so that a poorly chosen (colliding)
    /// description does not make distinct rules compare equal.
    /// </summary>
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
    /// An operand matching a node of type <typeparamref name="TNode"/> regardless of its children.
    /// </summary>
    protected static RuleOperand Any<TNode>()
        where TNode : INode
    {
        return new RuleOperand(typeof(TNode), RuleOperandChildPolicy.Any);
    }

    /// <summary>
    /// An operand matching a node of type <typeparamref name="TNode"/> that also satisfies a predicate,
    /// regardless of its children.
    /// </summary>
    protected static RuleOperand Any<TNode>(Func<INode, bool> predicate)
        where TNode : INode
    {
        return new RuleOperand(typeof(TNode), predicate, RuleOperandChildPolicy.Any);
    }

    /// <summary>
    /// An operand matching a leaf node of type <typeparamref name="TNode"/> (one with no children).
    /// </summary>
    protected static RuleOperand Leaf<TNode>()
        where TNode : INode
    {
        return new RuleOperand(typeof(TNode), RuleOperandChildPolicy.Leaf);
    }

    /// <summary>
    /// An operand matching a node of type <typeparamref name="TNode"/> whose children match the given
    /// child operands positionally.
    /// </summary>
    protected static RuleOperand Some<TNode>(params RuleOperand[] children)
        where TNode : INode
    {
        return new RuleOperand(typeof(TNode), RuleOperandChildPolicy.Some, children);
    }

    /// <summary>
    /// An operand matching a node of type <typeparamref name="TNode"/> whose children match the given
    /// child operands in any order.
    /// </summary>
    protected static RuleOperand Unordered<TNode>(params RuleOperand[] children)
        where TNode : INode
    {
        return new RuleOperand(typeof(TNode), RuleOperandChildPolicy.Unordered, children);
    }

    /// <summary>
    /// An operand matching a node of type <typeparamref name="TNode"/> that carries <paramref name="trait"/>
    /// — used to match a node needing conversion.
    /// </summary>
    protected static RuleOperand ConvertOperand<TNode>(ITrait trait)
        where TNode : INode
    {
        return new RuleOperand(typeof(TNode), trait, RuleOperandChildPolicy.Any);
    }

    // ~ Operand flattening -----------------------------------------------------

    /// <summary>
    /// Flattens the operand tree into a list in prefix order, wiring each operand's rule, parent, and
    /// ordinals as it goes.
    /// </summary>
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
