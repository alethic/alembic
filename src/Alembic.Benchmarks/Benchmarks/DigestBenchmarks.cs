using System.Collections.Generic;

using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using BenchmarkDotNet.Attributes;

namespace Alembic.Benchmarks;

/// <summary>
/// Isolates the digest hot path: structural hashing (<see cref="IOp.DeepHashCode"/>), deep equality
/// (<see cref="IOp.DeepEquals"/>), and keying a dictionary by <see cref="IOpDigest"/> the way the
/// planners do. <see cref="MemoryDiagnoser"/> reports the allocation cost we want to drive down.
/// </summary>
[MemoryDiagnoser]
public class DigestBenchmarks
{

    [Params(1000, 10000)]
    public int N;

    OpTraitSet _traits = null!;
    OpCluster _cluster = null!;
    IOp _leaf = null!;
    IOp _tree = null!;
    IOp _treeCopy = null!;
    IOp[] _leaves = null!;

    [GlobalSetup]
    public void Setup()
    {
        var planner = new VolcanoPlanner();
        _cluster = new OpCluster(planner);
        _traits = OpTraitSet.CreateEmpty();

        _leaf = new Var(_cluster, _traits, "a", 0);
        _tree = BuildTree(12);
        _treeCopy = BuildTree(12);

        _leaves = new IOp[N];
        for (int i = 0; i < N; i++)
            _leaves[i] = new Var(_cluster, _traits, "v" + i, i);
    }

    // A balanced binary tree of the given depth: 2^depth leaves, 2^depth-1 binary nodes.
    IOp BuildTree(int depth)
    {
        if (depth == 0)
            return new Var(_cluster, _traits, "x", depth);

        return new Bin(_traits, BuildTree(depth - 1), BuildTree(depth - 1));
    }

    // Fresh structural hash of a single leaf: one node's DigestItems allocation + boxing of the int term.
    [Benchmark]
    public int HashLeaf() => _leaf.DeepHashCode();

    // Fresh structural hash of a whole tree: recurses every node.
    [Benchmark]
    public int HashTree() => _tree.DeepHashCode();

    // Deep equality of two structurally-equal trees (the collision path).
    [Benchmark]
    public bool EqualsTree() => _tree.DeepEquals(_treeCopy);

    // Key N distinct ops into a digest-keyed dictionary, as the planner's _digestToOp does.
    [Benchmark]
    public int DigestMapInsert()
    {
        var map = new Dictionary<IOpDigest, IOp>();
        foreach (var op in _leaves)
            map[op.GetOpDigest()] = op;
        return map.Count;
    }

}
