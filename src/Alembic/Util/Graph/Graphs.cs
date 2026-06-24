using System.Collections.Generic;
using System.Linq;

using Alembic.Util;

namespace Alembic.Util.Graph;

/// <summary>
/// Helpers over <see cref="DirectedGraph{V, E}"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.Graphs")]
public static class Graphs
{

    /// <summary>
    /// The sources of the edges entering <paramref name="vertex"/> — its immediate predecessors. A
    /// predecessor appears once per edge, so a vertex referenced twice appears twice.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.Graphs", "predecessorListOf(DirectedGraph<V, E>, V)")]
    public static List<V> PredecessorListOf<V, E>(DirectedGraph<V, E> graph, V vertex)
        where E : DefaultEdge
    {
        var result = new List<V>();
        foreach (var edge in graph.GetInwardEdges(vertex))
            result.Add((V)edge.Source);

        return result;
    }

    /// <summary>
    /// Freezes <paramref name="graph"/> into an immutable view that precomputes the shortest distance
    /// between every pair of reachable vertices (a Bellman-Ford-style relaxation over the edges).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.Graphs", "makeImmutable(DirectedGraph<V, E>)")]
    public static FrozenGraph<V, E> MakeImmutable<V, E>(DirectedGraph<V, E> graph)
        where V : notnull
        where E : DefaultEdge
    {
        var graph1 = (DefaultDirectedGraph<V, E>)graph;
        var shortestDistances = new Dictionary<Pair<V, V>, int>();
        foreach (var edge in graph1.EdgeSet)
            shortestDistances[Pair.Of((V)edge.Source, (V)edge.Target)] = 1;

        while (true)
        {
            var previous = new List<Pair<V, V>>(shortestDistances.Keys);
            var changed = false;
            foreach (var edge in graph.EdgeSet)
                foreach (var edge2 in previous)
                {
                    if (((V)edge.Target).Equals(edge2.Left))
                    {
                        var key = Pair.Of((V)edge.Source, edge2.Right);
                        var arc2Distance = shortestDistances[edge2];
                        if (!shortestDistances.TryGetValue(key, out var bestDistance) || bestDistance > arc2Distance + 1)
                        {
                            shortestDistances[key] = arc2Distance + 1;
                            changed = true;
                        }
                    }
                }

            if (!changed)
                break;
        }

        return new FrozenGraph<V, E>(graph1, shortestDistances);
    }

    /// <summary>
    /// An immutable view of a graph that answers shortest-distance and all-paths queries between vertices.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.Graphs.FrozenGraph")]
    public sealed class FrozenGraph<V, E>
        where V : notnull
        where E : DefaultEdge
    {

        readonly DefaultDirectedGraph<V, E> _graph;
        readonly Dictionary<Pair<V, V>, int> _shortestDistances;

        /// <summary>
        /// Created by <see cref="Graphs.MakeImmutable"/> from a graph and its precomputed shortest distances.
        /// </summary>
        internal FrozenGraph(DefaultDirectedGraph<V, E> graph, Dictionary<Pair<V, V>, int> shortestDistances)
        {
            _graph = graph;
            _shortestDistances = shortestDistances;
        }

        /// <summary>
        /// All paths from <paramref name="from"/> to <paramref name="to"/>, in non-decreasing length order.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.Graphs.FrozenGraph", "getPaths(V, V)")]
        public List<List<V>> GetPaths(V from, V to)
        {
            var list = new List<List<V>>();
            if (from.Equals(to))
                list.Add(new List<V> { from });

            FindPaths(from, to, list);
            // Calcite sorts with Collections.sort, which is stable; OrderBy is the stable .NET analog, so
            // equal-length paths keep their discovery order (deterministic conversion-path selection).
            return list.OrderBy(p => p.Count).ToList();
        }

        /// <summary>
        /// The shortest distance from <paramref name="from"/> to <paramref name="to"/>, or -1 if unreachable.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.Graphs.FrozenGraph", "getShortestDistance(V, V)")]
        public int GetShortestDistance(V from, V to)
        {
            if (from.Equals(to))
                return 0;

            return _shortestDistances.TryGetValue(Pair.Of(from, to), out var distance) ? distance : -1;
        }

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.Graphs.FrozenGraph", "findPaths(V, V, List)")]
        void FindPaths(V from, V to, List<List<V>> list)
        {
            if (GetShortestDistance(from, to) == -1)
                return;

            var prefix = new List<V> { from };
            FindPathsExcluding(from, to, list, new HashSet<V>(), prefix);
        }

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.Graphs.FrozenGraph", "findPathsExcluding(V, V, List, Set, List)")]
        void FindPathsExcluding(V from, V to, List<List<V>> list, HashSet<V> excludedNodes, List<V> prefix)
        {
            excludedNodes.Add(from);
            foreach (var edge in _graph.EdgeSet)
            {
                if (((V)edge.Source).Equals(from))
                {
                    var target = (V)edge.Target;
                    if (target.Equals(to))
                    {
                        prefix.Add(target);
                        list.Add(new List<V>(prefix));
                        prefix.RemoveAt(prefix.Count - 1);
                    }
                    else if (excludedNodes.Contains(target))
                    {
                        // Already on the current path; skip to avoid a cycle.
                    }
                    else
                    {
                        prefix.Add(target);
                        FindPathsExcluding(target, to, list, excludedNodes, prefix);
                        prefix.RemoveAt(prefix.Count - 1);
                    }
                }
            }

            excludedNodes.Remove(from);
        }

    }

}
