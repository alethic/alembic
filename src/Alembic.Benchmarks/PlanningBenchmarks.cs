using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Expression;
using Alembic.Tests.Languages.Expression.Logical;

using BenchmarkDotNet.Attributes;

namespace Alembic.Benchmarks;

/// <summary>
/// An end-to-end Volcano planning run over the toy arithmetic-expression language: build a deep logical
/// tree, register the physical convention, and drive it to the best physical plan. This is the realistic
/// workload that keys ops into <c>_digestToOp</c> — <see cref="MemoryDiagnoser"/> shows the total
/// allocation of a full plan so we can see how much the digest path contributes.
/// </summary>
[MemoryDiagnoser]
public class PlanningBenchmarks
{

    // Depth of the balanced expression tree; 2^Depth leaves and 2^Depth-1 binary ops.
    [Params(4, 6)]
    public int Depth;

    static readonly string[] Names = { "a", "b", "c", "d" };

    [Benchmark]
    public IOp Plan()
    {
        var logical = OpTraitSet.CreateEmpty().Plus(ExpressionConventions.Logical);
        var physical = OpTraitSet.CreateEmpty().Plus(ExpressionConventions.Physical);

        var planner = new VolcanoPlanner();
        var cluster = new OpCluster(planner);

        int leaf = 0;
        IOp root = Build(cluster, logical, Depth, ref leaf);

        ExpressionConventions.Physical.Register(planner);
        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, physical));
        return planner.FindBestPlan();
    }

    // A balanced tree that alternates Multiply and Add levels over repeated Variable leaves; repeated
    // leaf names make structurally-equal subtrees collide in the digest map, exercising its dedup.
    static IOp Build(OpCluster cluster, OpTraitSet logical, int depth, ref int leaf)
    {
        if (depth == 0)
            return new Variable(cluster, logical, Names[leaf++ % Names.Length]);

        var left = Build(cluster, logical, depth - 1, ref leaf);
        var right = Build(cluster, logical, depth - 1, ref leaf);
        return (depth % 2 == 0)
            ? new Add(logical, left, right)
            : new Multiply(logical, left, right);
    }

}
