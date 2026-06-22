using System.Collections;
using System.Collections.Generic;

namespace Alembic.Util.Graph;

/// <summary>
/// Iterates the vertices reachable from a start vertex breadth-first.
/// </summary>
public sealed class BreadthFirstIterator<V, E> : IEnumerator<V>
    where V : notnull
    where E : DefaultEdge
{

    readonly DirectedGraph<V, E> _graph;
    readonly Queue<V> _queue = new Queue<V>();
    readonly HashSet<V> _set = new HashSet<V>();
    V _current = default!;

    public BreadthFirstIterator(DirectedGraph<V, E> graph, V root)
    {
        _graph = graph;
        _queue.Enqueue(root);
    }

    /// <summary>
    /// The vertices reachable from <paramref name="root"/>, in breadth-first order.
    /// </summary>
    public static IEnumerable<V> Of(DirectedGraph<V, E> graph, V root)
    {
        var iterator = new BreadthFirstIterator<V, E>(graph, root);
        while (iterator.MoveNext())
            yield return iterator.Current;
    }

    /// <summary>
    /// Populates <paramref name="set"/> with every vertex reachable from <paramref name="root"/>
    /// (including it).
    /// </summary>
    public static void Reachable(HashSet<V> set, DirectedGraph<V, E> graph, V root)
    {
        var queue = new Queue<V>();
        queue.Enqueue(root);
        set.Add(root);
        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            foreach (var edge in graph.GetOutwardEdges(v))
            {
                var target = (V)edge.Target;
                if (set.Add(target))
                    queue.Enqueue(target);
            }
        }
    }

    public V Current => _current;

    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
        if (_queue.Count == 0)
            return false;

        _current = _queue.Dequeue();
        foreach (var edge in _graph.GetOutwardEdges(_current))
        {
            var target = (V)edge.Target;
            if (_set.Add(target))
                _queue.Enqueue(target);
        }

        return true;
    }

    public void Reset() => throw new System.NotSupportedException();

    public void Dispose()
    {

    }

}
