using System.Collections.Generic;

namespace Alembic.Util.Graph;

/// <summary>
/// A directed graph: a set of vertices joined by directed edges.
/// </summary>
/// <typeparam name="V">The vertex type.</typeparam>
/// <typeparam name="E">The edge type.</typeparam>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph")]
public interface DirectedGraph<V, E>
    where E : DefaultEdge
{

    /// <summary>
    /// Adds a vertex; returns whether it was new.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph", "addVertex(V)")]
    bool AddVertex(V vertex);

    /// <summary>
    /// Adds an edge from <paramref name="vertex"/> to <paramref name="targetVertex"/>, returning the new
    /// edge, or <c>null</c> if one was already present.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph", "addEdge(V, V)")]
    E? AddEdge(V vertex, V targetVertex);

    /// <summary>
    /// The edge from <paramref name="source"/> to <paramref name="target"/>, or <c>null</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph", "getEdge(V, V)")]
    E? GetEdge(V source, V target);

    /// <summary>
    /// Removes the edge from <paramref name="vertex"/> to <paramref name="targetVertex"/>; returns
    /// whether one was removed.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph", "removeEdge(V, V)")]
    bool RemoveEdge(V vertex, V targetVertex);

    /// <summary>
    /// The vertices, as a set (iterating in insertion order).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph", "vertexSet()")]
    IReadOnlySet<V> VertexSet { get; }

    /// <summary>
    /// Removes every vertex in <paramref name="collection"/> and all edges into or out of them.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph", "removeAllVertices(Collection<V>)")]
    void RemoveAllVertices(IEnumerable<V> collection);

    /// <summary>
    /// The edges leaving <paramref name="source"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph", "getOutwardEdges(V)")]
    IReadOnlyList<E> GetOutwardEdges(V source);

    /// <summary>
    /// The edges entering <paramref name="vertex"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph", "getInwardEdges(V)")]
    IReadOnlyList<E> GetInwardEdges(V vertex);

    /// <summary>
    /// All edges, as a set.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph", "edgeSet()")]
    IReadOnlySet<E> EdgeSet { get; }

    /// <summary>
    /// Creates edges for a graph.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph.EdgeFactory")]
    interface EdgeFactory
    {

        /// <summary>
        /// Creates an edge from <paramref name="v0"/> to <paramref name="v1"/>.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DirectedGraph.EdgeFactory", "createEdge(V, V)")]
        E CreateEdge(V v0, V v1);

    }

}
