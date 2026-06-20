using System;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Hep;

/// <summary>
/// A heuristic planner: it applies the program's rules to a single tree, in the configured order,
/// re-passing until no rule changes anything. Deterministic and program-driven.
/// </summary>
/// <remarks>
/// This milestone rewrites the immutable tree directly, sharing untouched subtrees. A shared-DAG
/// deduplication (one vertex per equivalent subtree) is a planned optimization; the identity
/// machinery it needs — <see cref="INode.DeepEquals"/> / <see cref="INode.DeepHashCode"/> — is
/// already in place.
/// </remarks>
public sealed class HepPlanner : IPlanner
{

    const int DefaultPassLimit = 1024;

    readonly HepProgram _program;
    INode? _root;

    /// <summary>
    /// Creates a planner driven by the given program.
    /// </summary>
    public HepPlanner(HepProgram program)
    {
        _program = program;
    }

    /// <inheritdoc />
    public void SetRoot(INode node)
    {
        _root = node;
    }

    /// <inheritdoc />
    public INode FindBestPlan()
    {
        if (_root is null)
            throw new InvalidOperationException("No root has been set.");

        var current = _root;
        var limit = _program.MatchLimit == int.MaxValue ? DefaultPassLimit : _program.MatchLimit;

        for (int pass = 0; pass < limit; pass++)
        {
            var next = Rewrite(current);
            if (next.DeepEquals(current))
                break;

            current = next;
        }

        _root = current;
        return current;
    }

    INode Rewrite(INode node)
    {
        if (_program.MatchOrder == HepMatchOrder.TopDown)
        {
            node = ApplyRules(node);
            node = RewriteChildren(node);
        }
        else
        {
            node = RewriteChildren(node);
            node = ApplyRules(node);
        }

        return node;
    }

    INode RewriteChildren(INode node)
    {
        var children = node.Children;
        if (children.IsEmpty)
            return node;

        ImmutableArray<INode>.Builder? builder = null;
        for (int i = 0; i < children.Length; i++)
        {
            var rewritten = Rewrite(children[i]);
            if (!ReferenceEquals(rewritten, children[i]))
            {
                builder ??= children.ToBuilder();
                builder[i] = rewritten;
            }
        }

        return builder is null ? node : node.Copy(node.Traits, builder.ToImmutable());
    }

    INode ApplyRules(INode node)
    {
        foreach (var rule in _program.Rules)
        {
            if (!TryMatch(rule, node, out var call))
                continue;

            if (!rule.Matches(call))
                continue;

            rule.OnMatch(call);

            if (call.Result is not null && !call.Result.DeepEquals(node))
                return call.Result;
        }

        return node;
    }

    static bool TryMatch(IRule rule, INode node, out RuleCall call)
    {
        var bound = ImmutableArray.CreateBuilder<INode>();
        if (MatchOperand(rule.Operand, node, bound))
        {
            call = new RuleCall(bound.ToImmutable());
            return true;
        }

        call = null!;
        return false;
    }

    static bool MatchOperand(Operand operand, INode node, ImmutableArray<INode>.Builder bound)
    {
        if (!operand.Matches(node))
            return false;

        bound.Add(node);

        if (operand.Children.IsEmpty)
            return true;

        if (node.Children.Length != operand.Children.Length)
            return false;

        for (int i = 0; i < operand.Children.Length; i++)
        {
            if (!MatchOperand(operand.Children[i], node.Children[i], bound))
                return false;
        }

        return true;
    }

}
