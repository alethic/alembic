using System;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Hep;

/// <summary>
/// A heuristic planner: it applies its rules to a single tree, in the configured order, re-passing
/// until no rule changes anything. Deterministic and program-driven.
/// </summary>
/// <remarks>
/// It rewrites the immutable tree directly, sharing untouched subtrees by reference. A shared-DAG
/// deduplication is a planned optimization; the identity machinery it needs — <see cref="INode.DeepEquals"/>
/// / <see cref="INode.DeepHashCode"/> — is already in place.
/// </remarks>
public sealed class HepPlanner : AbstractPlanner
{

    const int DefaultPassLimit = 1024;

    readonly HepMatchOrder _matchOrder;
    readonly int _matchLimit;
    INode? _root;
    TraitSet? _requestedRootTraits;

    /// <summary>
    /// Creates a planner seeded with the program's rules, order, and limit. More rules can be added
    /// with <see cref="AbstractPlanner.AddRule"/> (e.g. by a convention's <see cref="ITrait.Register"/>).
    /// </summary>
    public HepPlanner(HepProgram program)
    {
        _matchOrder = program.MatchOrder;
        _matchLimit = program.MatchLimit;

        foreach (var rule in program.Rules)
            AddRule(rule);
    }

    /// <inheritdoc />
    public override void SetRoot(INode node)
    {
        _root = node;
    }

    /// <summary>
    /// Records the traits the final plan must carry. Like the heuristic planner it models, only the
    /// root's request is remembered; it is enforced when <see cref="FindBestPlan"/> finishes.
    /// </summary>
    public override INode ChangeTraits(INode node, TraitSet toTraits)
    {
        if (ReferenceEquals(node, _root))
            _requestedRootTraits = toTraits;

        return node;
    }

    /// <inheritdoc />
    public override INode FindBestPlan()
    {
        if (_root is null)
            throw new InvalidOperationException("No root has been set.");

        var current = _root;
        var limit = _matchLimit == int.MaxValue ? DefaultPassLimit : _matchLimit;

        for (int pass = 0; pass < limit; pass++)
        {
            var next = Rewrite(current);
            if (next.DeepEquals(current))
                break;

            current = next;
        }

        _root = current;

        if (_requestedRootTraits is not null)
            EnsureSatisfies(current, _requestedRootTraits);

        return current;
    }

    /// <summary>
    /// Verifies that every node in the plan carries the requested traits. Because conversions rewrite
    /// nodes in place rather than wrapping them, a complete plan is uniform throughout, so a single
    /// surviving node that falls short means no rule chain could finish the job.
    /// </summary>
    static void EnsureSatisfies(INode node, TraitSet required)
    {
        if (!node.Traits.Satisfies(required))
            throw new CannotPlanException($"No plan satisfies the requested traits; '{node.GetType().Name}' remained in convention '{node.Convention}'.");

        foreach (var child in node.Children)
            EnsureSatisfies(child, required);
    }

    INode Rewrite(INode node)
    {
        if (_matchOrder == HepMatchOrder.TopDown)
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
        foreach (var rule in Rules)
        {
            var bound = OperandMatcher.Match(rule.Operand, node);
            if (bound is null)
                continue;

            var call = new HepRuleCall(bound.Value);
            rule.OnMatch(call);

            if (call.HasResult && !call.Result.DeepEquals(node))
                return call.Result;
        }

        return node;
    }

}
