using System.Collections;
using System.Collections.Generic;

using Alembic.Plan.Hep;

namespace Alembic.Util.Graph;

/// <summary>
/// Iterates the vertices in topological order: top-down yields a parent before its children, bottom-up
/// yields a child before its parents. Also serves as its own iterable: enumerating it produces a fresh
/// iterator.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.TopologicalOrderIterator")]
public class TopologicalOrderIterator<V, E> : IEnumerator<V>
    where V : notnull
    where E : DefaultEdge
{

    readonly DirectedGraph<V, E> _graph;
    readonly HepMatchOrder _order;
    readonly Dictionary<V, int> _count = new Dictionary<V, int>();
    readonly Queue<V> _empties = new Queue<V>();
    V _current = default!;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.TopologicalOrderIterator", "TopologicalOrderIterator(DirectedGraph<V, E>)")]
    public TopologicalOrderIterator(DirectedGraph<V, E> graph)
        : this(graph, HepMatchOrder.TopDown)
    {
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.TopologicalOrderIterator", "TopologicalOrderIterator(DirectedGraph<V, E>, HepMatchOrder)")]
    public TopologicalOrderIterator(DirectedGraph<V, E> graph, HepMatchOrder order)
    {
        _graph = graph;
        _order = order;
        Populate();
    }

    /// <summary>
    /// The vertices in topological order, parents before children.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.TopologicalOrderIterator", "of(DirectedGraph<V, E>)")]
    public static IEnumerable<V> Of(DirectedGraph<V, E> graph)
    {
        return Of(graph, HepMatchOrder.TopDown);
    }

    /// <summary>
    /// The vertices in topological order for the given <paramref name="order"/> (which must be
    /// <see cref="HepMatchOrder.TopDown"/> or <see cref="HepMatchOrder.BottomUp"/>).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.TopologicalOrderIterator", "of(DirectedGraph<V, E>, HepMatchOrder)")]
    public static IEnumerable<V> Of(DirectedGraph<V, E> graph, HepMatchOrder order)
    {
        var iterator = new TopologicalOrderIterator<V, E>(graph, order);
        while (iterator.MoveNext())
            yield return iterator.Current;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.TopologicalOrderIterator", "populate(Map<V, int[]>, List<V>)")]
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

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.TopologicalOrderIterator", "next()")]
    public V Current => _current;

    object IEnumerator.Current => Current!;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.TopologicalOrderIterator", "hasNext()")]
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

    /// <summary>
    /// Drains the iterator and returns the vertices that were never emitted — those still carrying
    /// unsatisfied incoming edges, i.e. the vertices that lie on a cycle.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.TopologicalOrderIterator", "findCycles()")]
    public IReadOnlyCollection<V> FindCycles()
    {
        while (MoveNext())
        {
        }

        return new HashSet<V>(_count.Keys);
    }

    public void Reset() => throw new System.NotSupportedException();

    public void Dispose()
    {
    }

}
