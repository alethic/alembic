using System.Collections;
using System.Collections.Generic;

using Alembic.Plan.Hep;

namespace Alembic.Util.Graph;

/// <summary>
/// Iterates the vertices in topological order: top-down yields a parent before its children, bottom-up
/// yields a child before its parents. Also serves as its own iterable: enumerating it produces a fresh
/// iterator.
/// </summary>
public sealed class TopologicalOrderIterator<V, E> : IEnumerator<V>
    where V : notnull
    where E : DefaultEdge
{

    readonly DirectedGraph<V, E> _graph;
    readonly HepMatchOrder _order;
    readonly Dictionary<V, int> _count = new Dictionary<V, int>();
    readonly Queue<V> _empties = new Queue<V>();
    V _current = default!;

    public TopologicalOrderIterator(DirectedGraph<V, E> graph)
        : this(graph, HepMatchOrder.TopDown)
    {
    }

    public TopologicalOrderIterator(DirectedGraph<V, E> graph, HepMatchOrder order)
    {
        _graph = graph;
        _order = order;
        Populate();
    }

    /// <summary>
    /// The vertices in topological order, parents before children.
    /// </summary>
    public static IEnumerable<V> Of(DirectedGraph<V, E> graph)
    {
        return Of(graph, HepMatchOrder.TopDown);
    }

    /// <summary>
    /// The vertices in topological order for the given <paramref name="order"/> (which must be
    /// <see cref="HepMatchOrder.TopDown"/> or <see cref="HepMatchOrder.BottomUp"/>).
    /// </summary>
    public static IEnumerable<V> Of(DirectedGraph<V, E> graph, HepMatchOrder order)
    {
        var iterator = new TopologicalOrderIterator<V, E>(graph, order);
        while (iterator.MoveNext())
            yield return iterator.Current;
    }

    void Populate()
    {
        foreach (var vertex in _graph.VertexSet)
            _count[vertex] = 0;

        foreach (var vertex in _graph.VertexSet)
        {
            foreach (var edge in _graph.GetOutwardEdges(vertex))
            {
                var key = _order == HepMatchOrder.TopDown ? (V)edge.Target : (V)edge.Source;
                _count[key]++;
            }
        }

        var zeros = new List<V>();
        foreach (var pair in _count)
            if (pair.Value == 0)
                zeros.Add(pair.Key);

        foreach (var zero in zeros)
        {
            _count.Remove(zero);
            _empties.Enqueue(zero);
        }
    }

    public V Current => _current;

    object IEnumerator.Current => Current!;

    public bool MoveNext()
    {
        if (_empties.Count == 0)
            return false;

        _current = _empties.Dequeue();
        var edges = _order == HepMatchOrder.TopDown ? _graph.GetOutwardEdges(_current) : _graph.GetInwardEdges(_current);
        foreach (var edge in edges)
        {
            var key = _order == HepMatchOrder.TopDown ? (V)edge.Target : (V)edge.Source;
            if (!_count.TryGetValue(key, out var remaining))
                continue;

            remaining--;
            if (remaining == 0)
            {
                _count.Remove(key);
                _empties.Enqueue(key);
            }
            else
            {
                _count[key] = remaining;
            }
        }

        return true;
    }

    public void Reset() => throw new System.NotSupportedException();

    public void Dispose()
    {
    }

}
