using System.Collections.Generic;

namespace Alembic.Util.Graph;

/// <summary>
/// Helpers over <see cref="DirectedGraph{V, E}"/>.
/// </summary>
[Provenance("org.apache.calcite.util.graph.Graphs")]
public static class Graphs
{

    /// <summary>
    /// The sources of the edges entering <paramref name="vertex"/> — its immediate predecessors. A
    /// predecessor appears once per edge, so a vertex referenced twice appears twice.
    /// </summary>
    [Provenance("org.apache.calcite.util.graph.Graphs", "predecessorListOf(DirectedGraph<V, E>, V)")]
    public static List<V> PredecessorListOf<V, E>(DirectedGraph<V, E> graph, V vertex)
        where E : DefaultEdge
    {
        var result = new List<V>();
        foreach (var edge in graph.GetInwardEdges(vertex))
            result.Add((V)edge.Source);

        return result;
    }

}
