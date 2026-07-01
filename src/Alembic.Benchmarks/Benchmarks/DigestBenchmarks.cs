using System.Collections.Generic;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

namespace Alembic.Benchmarks;

/// <summary>
/// Isolates the digest hot path: structural hashing (<see cref="IOp.DeepHashCode"/>), deep equality
/// (<see cref="IOp.DeepEquals"/>), and keying a dictionary by <see cref="IOpDigest"/> the way the
/// planners do.
/// </summary>
public class DigestBenchmarks
{

    OpTraitSet _traits = null!;
    OpCluster _cluster = null!;
    IOp _leaf = null!;
    IOp _tree = null!;
    IOp _treeCopy = null!;
    IOp[] _leaves = null!;

    public void Run(int mapSize = 1000)
    {
        var planner = new VolcanoPlanner();
        _cluster = new OpCluster(planner);
        _traits = OpTraitSet.CreateEmpty();

        _leaf = new Var(_cluster, _traits, "a", 0);
        _tree = BuildTree(12);
        _treeCopy = BuildTree(12);

        _leaves = new IOp[mapSize];
        for (int i = 0; i < mapSize; i++)
            _leaves[i] = new Var(_cluster, _traits, "v" + i, i);

        Bench.Header("Digest");
        Bench.Run("HashLeaf", 2_000_000, () => Bench.Sink += _leaf.DeepHashCode());
        Bench.Run("HashTree", 500, () => Bench.Sink += _tree.DeepHashCode());
        Bench.Run("EqualsTree", 300, () => Bench.Sink += _tree.DeepEquals(_treeCopy) ? 1 : 0);
        Bench.Run("DigestMapInsert", 2_000, () => Bench.Sink += DigestMapInsert());
    }

    // A balanced binary tree of the given depth: 2^depth leaves, 2^depth-1 binary nodes.
    IOp BuildTree(int depth)
    {
        if (depth == 0)
            return new Var(_cluster, _traits, "x", depth);

        return new Bin(_traits, BuildTree(depth - 1), BuildTree(depth - 1));
    }

    // Key N distinct ops into a digest-keyed dictionary, as the planner's _digestToOp does.
    int DigestMapInsert()
    {
        var map = new Dictionary<IOpDigest, IOp>();
        foreach (var op in _leaves)
            map[op.GetOpDigest()] = op;
        return map.Count;
    }

}
