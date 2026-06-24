using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

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

    // Calcite uses a LinkedHashSet here so edge iteration (e.g. when freezing the graph for conversion-
    // path search) is deterministic; our LinkedHashSet is the insertion-ordered analog.
    readonly Alembic.Util.LinkedHashSet<E> _edges = new Alembic.Util.LinkedHashSet<E>();
    readonly Dictionary<V, VertexInfo> _vertexMap = new Dictionary<V, VertexInfo>();
    readonly List<V> _order = new List<V>();
    readonly DirectedGraph<V, E>.EdgeFactory _edgeFactory;
    readonly VertexSetView _vertexSet;

    /// <summary>
    /// Creates an empty graph whose edges are produced by <paramref name="edgeFactory"/>.
    /// </summary>
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
        var outRemoved = false;
        for (int i = 0; i < outEdges.Count; i++)
        {
            if (outEdges[i].Target.Equals(target))
            {
                _edges.Remove(outEdges[i]);
                outEdges.RemoveAt(i);
                outRemoved = true;
                break;
            }
        }

        var inEdges = GetVertex(target).InEdges;
        var inRemoved = false;
        for (int i = 0; i < inEdges.Count; i++)
        {
            if (inEdges[i].Source.Equals(source))
            {
                inEdges.RemoveAt(i);
                inRemoved = true;
                break;
            }
        }

        Debug.Assert(outRemoved == inRemoved);
        return outRemoved;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "vertexSet()")]
    public IReadOnlySet<V> VertexSet => _vertexSet;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultDirectedGraph", "removeAllVertices(Collection<V>)")]
    public void RemoveAllVertices(IEnumerable<V> collection)
    {
        var coll = collection as ICollection<V> ?? new List<V>(collection);

        // The point at which the collection is large enough to make the 'majority' algorithm cheaper.
        const float threshold = 0.35f;
        int thresholdSize = (int)(_vertexMap.Count * threshold);
        if (coll.Count > thresholdSize && coll is not ISet<V>)
            coll = new HashSet<V>(coll); // faster Contains; also collapses any duplicates

        if (coll.Count > thresholdSize)
            RemoveMajorityVertices((ISet<V>)coll);
        else
            RemoveMinorityVertices(coll);

        // remove all edges referencing a removed vertex from the global edge set
        foreach (var v in coll)
            _edges.RemoveWhere(e => e.Source.Equals(v) || e.Target.Equals(v));
    }

    void RemoveMinorityVertices(ICollection<V> collection)
    {
        foreach (var v in collection)
        {
            if (!_vertexMap.TryGetValue(v, out var info))
                continue;

            // remove all edges pointing to v
            foreach (var edge in info.InEdges)
                GetVertex((V)edge.Source).OutEdges.RemoveAll(e => e.Target.Equals(v));

            // remove all edges starting from v
            foreach (var edge in info.OutEdges)
                GetVertex((V)edge.Target).InEdges.RemoveAll(e => e.Source.Equals(v));
        }

        foreach (var v in collection)
            if (_vertexMap.Remove(v))
                _order.Remove(v);
    }

    void RemoveMajorityVertices(ISet<V> vertexSet)
    {
        foreach (var v in vertexSet)
            if (_vertexMap.Remove(v))
                _order.Remove(v);

        foreach (var info in _vertexMap.Values)
        {
            info.OutEdges.RemoveAll(e => vertexSet.Contains((V)e.Target));
            info.InEdges.RemoveAll(e => vertexSet.Contains((V)e.Source));
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

    /// <summary>
    /// Returns the recorded <see cref="VertexInfo"/> for <paramref name="vertex"/>, throwing if it is not
    /// a vertex of the graph.
    /// </summary>
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

        /// <summary>
        /// Creates a view over the vertex <paramref name="order"/> list and membership <paramref name="map"/>.
        /// </summary>
        public VertexSetView(List<V> order, Dictionary<V, VertexInfo> map)
        {
            _order = order;
            _map = map;
        }

        /// <inheritdoc/>
        public int Count => _order.Count;

        /// <inheritdoc/>
        public bool Contains(V item) => _map.ContainsKey(item);

        /// <inheritdoc/>
        public IEnumerator<V> GetEnumerator() => _order.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _order.GetEnumerator();

        /// <inheritdoc/>
        public bool IsProperSubsetOf(IEnumerable<V> other) => Snapshot().IsProperSubsetOf(other);

        /// <inheritdoc/>
        public bool IsProperSupersetOf(IEnumerable<V> other) => Snapshot().IsProperSupersetOf(other);

        /// <inheritdoc/>
        public bool IsSubsetOf(IEnumerable<V> other) => Snapshot().IsSubsetOf(other);

        /// <inheritdoc/>
        public bool IsSupersetOf(IEnumerable<V> other) => Snapshot().IsSupersetOf(other);

        /// <inheritdoc/>
        public bool Overlaps(IEnumerable<V> other) => Snapshot().Overlaps(other);

        /// <inheritdoc/>
        public bool SetEquals(IEnumerable<V> other) => Snapshot().SetEquals(other);

        HashSet<V> Snapshot() => new HashSet<V>(_order);

    }

}
