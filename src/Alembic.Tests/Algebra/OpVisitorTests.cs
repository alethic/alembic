using System.Collections.Generic;
using System.Linq;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Expression;
using Alembic.Tests.Languages.Expression.Logical;

using Xunit;

namespace Alembic.Tests.Algebra;

/// <summary>
/// Exercises <see cref="OpVisitor"/>: depth-first descent via <see cref="IOp.ChildrenAccept"/>, the root
/// returned by <see cref="OpVisitor.Go"/>, and <see cref="OpVisitor.ReplaceRoot"/>.
/// </summary>
public class OpVisitorTests
{

    // (a * b) + c
    static (IOp Root, IOp Mul, IOp A, IOp B, IOp C) Tree()
    {
        var cluster = new OpCluster(new VolcanoPlanner());
        var t = OpTraitSet.CreateEmpty().Plus(ExpressionConventions.Logical);
        var a = new Variable(cluster, t, "a");
        var b = new Variable(cluster, t, "b");
        var mul = new Multiply(t, a, b);
        var c = new Variable(cluster, t, "c");
        IOp root = new Add(t, mul, c);
        return (root, mul, a, b, c);
    }

    sealed class Collector : OpVisitor
    {

        public List<(IOp Op, int Ordinal, IOp? Parent)> Visited { get; } = new List<(IOp, int, IOp?)>();

        public override void Visit(IOp node, int ordinal, IOp? parent)
        {
            Visited.Add((node, ordinal, parent));
            base.Visit(node, ordinal, parent);
        }

    }

    [Fact]
    public void Go_visits_depth_first_and_returns_the_root()
    {
        var (root, mul, a, b, c) = Tree();
        var collector = new Collector();

        var result = collector.Go(root);

        Assert.Same(root, result);
        // Pre-order: the op, then its children left-to-right.
        Assert.Equal(new[] { root, mul, a, b, c }, collector.Visited.Select(v => v.Op).ToArray());
        Assert.Equal((root, 0, (IOp?)null), collector.Visited[0]);
        Assert.Equal((mul, 0, root), collector.Visited[1]);
        Assert.Equal((a, 0, mul), collector.Visited[2]);
        Assert.Equal((b, 1, mul), collector.Visited[3]);
        Assert.Equal((c, 1, root), collector.Visited[4]);
    }

    // Swaps the tree's root for a replacement when it reaches the root.
    sealed class RootReplacer : OpVisitor
    {

        readonly IOp _replacement;

        public RootReplacer(IOp replacement) => _replacement = replacement;

        public override void Visit(IOp node, int ordinal, IOp? parent)
        {
            if (parent is null)
                ReplaceRoot(_replacement);
        }

    }

    [Fact]
    public void ReplaceRoot_changes_what_Go_returns()
    {
        var (root, mul, _, _, _) = Tree();

        var result = new RootReplacer(mul).Go(root);

        Assert.Same(mul, result);
    }

}
