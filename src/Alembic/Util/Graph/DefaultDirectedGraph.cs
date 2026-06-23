using System.Collections;
using System.Collections.Generic;

namespace Alembic.Util.Graph;

/// <summary>
/// The default <see cref="DirectedGraph{V, E}"/> implementation. Vertices and per-vertex edges keep
/// insertion order, so traversals are deterministic.
/// </summary>
/// <typeparam name="V">The vertex type.</typeparam>
/// <typeparam name="E">The edge type.</typeparam>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph")]
public class DefaultDirectedGraph<V, E> : DirectedGraph<V, E>
    where V : notnull
    where E : DefaultEdge
{

    readonly HashSet<E> _edges = new HashSet<E>();
    readonly Dictionary<V, VertexInfo> _vertexMap = new Dictionary<V, VertexInfo>();
    readonly List<V> _order = new List<V>();
    readonly DirectedGraph<V, E>.EdgeFactory _edgeFactory;
    readonly VertexSetView _vertexSet;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "DefaultDirectedGraph(EdgeFactory)")]
    public DefaultDirectedGraph(DirectedGraph<V, E>.EdgeFactory edgeFactory)
    {
        _edgeFactory = edgeFactory;
        _vertexSet = new VertexSetView(_order, _vertexMap);
    }

    /// <summary>
    /// Creates a graph of plain <see cref="DefaultEdge"/>s.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "create()")]
    public static DefaultDirectedGraph<V, DefaultEdge> Create()
    {
        return new DefaultDirectedGraph<V, DefaultEdge>(DefaultEdge.Factory<V>());
    }

    /// <summary>
    /// Creates a graph with the given edge factory.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "create(EdgeFactory)")]
    public static DefaultDirectedGraph<V, E> Create(DirectedGraph<V, E>.EdgeFactory edgeFactory)
    {
        return new DefaultDirectedGraph<V, E>(edgeFactory);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "addVertex(V)")]
    public bool AddVertex(V vertex)
    {
        if (_vertexMap.ContainsKey(vertex))
            return false;

        _vertexMap.Add(vertex, new VertexInfo());
        _order.Add(vertex);
        return true;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "addEdge(V, V)")]
    public E? AddEdge(V vertex, V targetVertex)
    {
        var info = GetVertex(vertex);
        var targetInfo = GetVertex(targetVertex);
        var edge = _edgeFactory.CreateEdge(vertex, targetVertex);
        if (_edges.Add(edge))
        {
            info.OutEdges.Add(edge);
            targetInfo.InEdges.Add(edge);
            return edge;
        }

        return null;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "getEdge(V, V)")]
    public E? GetEdge(V source, V target)
    {
        foreach (var outEdge in GetVertex(source).OutEdges)
            if (outEdge.Target.Equals(target))
                return outEdge;

        return null;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "removeEdge(V, V)")]
    public bool RemoveEdge(V source, V target)
    {
        var outEdges = GetVertex(source).OutEdges;
        var removed = false;
        for (int i = 0; i < outEdges.Count; i++)
        {
            if (outEdges[i].Target.Equals(target))
            {
                _edges.Remove(outEdges[i]);
                outEdges.RemoveAt(i);
                removed = true;
                break;
            }
        }

        var inEdges = GetVertex(target).InEdges;
        for (int i = 0; i < inEdges.Count; i++)
        {
            if (inEdges[i].Source.Equals(source))
            {
                inEdges.RemoveAt(i);
                break;
            }
        }

        return removed;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "vertexSet()")]
    public IReadOnlySet<V> VertexSet => _vertexSet;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "removeAllVertices(Collection<V>)")]
    public void RemoveAllVertices(IEnumerable<V> collection)
    {
        var set = collection as HashSet<V> ?? new HashSet<V>(collection);
        foreach (var vertex in set)
        {
            if (_vertexMap.Remove(vertex))
                _order.Remove(vertex);
        }

        _edges.RemoveWhere(e => set.Contains((V)e.Source) || set.Contains((V)e.Target));
        foreach (var info in _vertexMap.Values)
        {
            info.InEdges.RemoveAll(e => set.Contains((V)e.Source));
            info.OutEdges.RemoveAll(e => set.Contains((V)e.Target));
        }
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "getOutwardEdges(V)")]
    public IReadOnlyList<E> GetOutwardEdges(V source)
    {
        return GetVertex(source).OutEdges;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "getInwardEdges(V)")]
    public IReadOnlyList<E> GetInwardEdges(V vertex)
    {
        return GetVertex(vertex).InEdges;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "edgeSet()")]
    public IReadOnlySet<E> EdgeSet => _edges;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "getVertex(V)")]
    protected VertexInfo GetVertex(V vertex)
    {
        if (!_vertexMap.TryGetValue(vertex, out var info))
            throw new System.ArgumentException("no vertex " + vertex);

        return info;
    }

    /// <summary>
    /// The in- and out-edges recorded for a vertex.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph.VertexInfo")]
    protected sealed class VertexInfo
    {

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph.VertexInfo", "inEdges")]
        internal readonly List<E> InEdges = new List<E>();

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph.VertexInfo", "outEdges")]
        internal readonly List<E> OutEdges = new List<E>();

    }

    /// <summary>
    /// A read-only set view of the vertices: membership is O(1) via the vertex map, iteration follows
    /// insertion order.
    /// </summary>
    sealed class VertexSetView : IReadOnlySet<V>
    {

        readonly List<V> _order;
        readonly Dictionary<V, VertexInfo> _map;

        public VertexSetView(List<V> order, Dictionary<V, VertexInfo> map)
        {
            _order = order;
            _map = map;
        }

        public int Count => _order.Count;

        public bool Contains(V item) => _map.ContainsKey(item);

        public IEnumerator<V> GetEnumerator() => _order.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _order.GetEnumerator();

        public bool IsProperSubsetOf(IEnumerable<V> other) => Snapshot().IsProperSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<V> other) => Snapshot().IsProperSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<V> other) => Snapshot().IsSubsetOf(other);

        public bool IsSupersetOf(IEnumerable<V> other) => Snapshot().IsSupersetOf(other);

        public bool Overlaps(IEnumerable<V> other) => Snapshot().Overlaps(other);

        public bool SetEquals(IEnumerable<V> other) => Snapshot().SetEquals(other);

        HashSet<V> Snapshot() => new HashSet<V>(_order);

    }

}
