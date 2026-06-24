using System.Collections;
using System.Collections.Generic;

namespace Alembic.Util.Graph;

/// <summary>
/// Iterates the vertices reachable from a start vertex in depth-first, pre-order.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DepthFirstIterator")]
public class DepthFirstIterator<V, E> : IEnumerator<V>
    where V : notnull
    where E : DefaultEdge
{

    readonly IEnumerator<V> _iterator;

    /// <summary>
    /// Creates an iterator over the vertices of <paramref name="graph"/> reachable from <paramref name="start"/>,
    /// in depth-first pre-order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DepthFirstIterator", "DepthFirstIterator(DirectedGraph<V, E>, V)")]
    public DepthFirstIterator(DirectedGraph<V, E> graph, V start)
    {
        // Build the list up front and iterate it.
        _iterator = BuildList(graph, start).GetEnumerator();
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DepthFirstIterator", "buildList(DirectedGraph<V, E>, V)")]
    static List<V> BuildList(DirectedGraph<V, E> graph, V start)
    {
        var list = new List<V>();
        BuildListRecurse(list, new HashSet<V>(), graph, start);
        return list;
    }

    /// <summary>
    /// The vertices reachable from <paramref name="start"/>, in depth-first pre-order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DepthFirstIterator", "of(DirectedGraph<V, E>, V)")]
    public static IEnumerable<V> Of(DirectedGraph<V, E> graph, V start)
    {
        var iterator = new DepthFirstIterator<V, E>(graph, start);
        while (iterator.MoveNext())
            yield return iterator.Current;
    }

    /// <summary>
    /// Populates a collection with the vertices reachable from <paramref name="start"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DepthFirstIterator", "reachable(Collection<V>, DirectedGraph<V, E>, V)")]
    public static void Reachable(ICollection<V> list, DirectedGraph<V, E> graph, V start)
    {
        BuildListRecurse(list, new HashSet<V>(), graph, start);
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DepthFirstIterator", "buildListRecurse(Collection<V>, Set<V>, DirectedGraph<V, E>, V)")]
    static void BuildListRecurse(ICollection<V> list, HashSet<V> active, DirectedGraph<V, E> graph, V start)
    {
        if (!active.Add(start))
            return;

        list.Add(start);
        foreach (var edge in graph.GetOutwardEdges(start))
            BuildListRecurse(list, active, graph, (V)edge.Target);

        active.Remove(start);
    }

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DepthFirstIterator", "next()")]
    public V Current => _iterator.Current;

    object IEnumerator.Current => Current!;

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DepthFirstIterator", "hasNext()")]
    public bool MoveNext() => _iterator.MoveNext();

    /// <inheritdoc/>
    public void Reset() => throw new System.NotSupportedException();

    /// <inheritdoc/>
    public void Dispose() => _iterator.Dispose();

}
