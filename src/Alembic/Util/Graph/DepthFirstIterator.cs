using System.Collections;
using System.Collections.Generic;

namespace Alembic.Util.Graph;

/// <summary>
/// Iterates the vertices reachable from a start vertex in depth-first, pre-order.
/// </summary>
public sealed class DepthFirstIterator<V, E> : IEnumerator<V>
    where V : notnull
    where E : DefaultEdge
{

    readonly IEnumerator<V> _iterator;

    public DepthFirstIterator(DirectedGraph<V, E> graph, V start)
    {
        // Build the list up front and iterate it.
        _iterator = BuildList(graph, start).GetEnumerator();
    }

    static List<V> BuildList(DirectedGraph<V, E> graph, V start)
    {
        var list = new List<V>();
        BuildListRecurse(list, new HashSet<V>(), graph, start);
        return list;
    }

    /// <summary>
    /// The vertices reachable from <paramref name="start"/>, in depth-first pre-order.
    /// </summary>
    public static IEnumerable<V> Of(DirectedGraph<V, E> graph, V start)
    {
        var iterator = new DepthFirstIterator<V, E>(graph, start);
        while (iterator.MoveNext())
            yield return iterator.Current;
    }

    /// <summary>
    /// Populates a collection with the vertices reachable from <paramref name="start"/>.
    /// </summary>
    public static void Reachable(ICollection<V> list, DirectedGraph<V, E> graph, V start)
    {
        BuildListRecurse(list, new HashSet<V>(), graph, start);
    }

    static void BuildListRecurse(ICollection<V> list, HashSet<V> active, DirectedGraph<V, E> graph, V start)
    {
        if (!active.Add(start))
            return;

        list.Add(start);
        foreach (var edge in graph.GetOutwardEdges(start))
            BuildListRecurse(list, active, graph, (V)edge.Target);

        active.Remove(start);
    }

    public V Current => _iterator.Current;

    object IEnumerator.Current => Current!;

    public bool MoveNext() => _iterator.MoveNext();

    public void Reset() => throw new System.NotSupportedException();

    public void Dispose() => _iterator.Dispose();

}
