using System.Collections.Generic;

namespace Alembic.Util.Graph;

/// <summary>
/// A directed graph: a set of vertices joined by directed edges.
/// </summary>
/// <typeparam name="V">The vertex type.</typeparam>
/// <typeparam name="E">The edge type.</typeparam>
public interface DirectedGraph<V, E>
    where E : DefaultEdge
{

    /// <summary>
    /// Adds a vertex; returns whether it was new.
    /// </summary>
    bool AddVertex(V vertex);

    /// <summary>
    /// Adds an edge from <paramref name="vertex"/> to <paramref name="targetVertex"/>, returning the new
    /// edge, or <c>null</c> if one was already present.
    /// </summary>
    E? AddEdge(V vertex, V targetVertex);

    /// <summary>
    /// The edge from <paramref name="source"/> to <paramref name="target"/>, or <c>null</c>.
    /// </summary>
    E? GetEdge(V source, V target);

    /// <summary>
    /// Removes the edge from <paramref name="vertex"/> to <paramref name="targetVertex"/>; returns
    /// whether one was removed.
    /// </summary>
    bool RemoveEdge(V vertex, V targetVertex);

    /// <summary>
    /// The vertices, as a set (iterating in insertion order).
    /// </summary>
    IReadOnlySet<V> VertexSet { get; }

    /// <summary>
    /// Removes every vertex in <paramref name="collection"/> and all edges into or out of them.
    /// </summary>
    void RemoveAllVertices(IEnumerable<V> collection);

    /// <summary>
    /// The edges leaving <paramref name="source"/>.
    /// </summary>
    IReadOnlyList<E> GetOutwardEdges(V source);

    /// <summary>
    /// The edges entering <paramref name="vertex"/>.
    /// </summary>
    IReadOnlyList<E> GetInwardEdges(V vertex);

    /// <summary>
    /// All edges, as a set.
    /// </summary>
    IReadOnlySet<E> EdgeSet { get; }

    /// <summary>
    /// Creates edges for a graph.
    /// </summary>
    interface EdgeFactory
    {

        E CreateEdge(V v0, V v1);

    }

}
