namespace Alembic.Util.Graph;

/// <summary>
/// A directed edge in a <see cref="DirectedGraph{V, E}"/>, from <see cref="Source"/> to
/// <see cref="Target"/>. Two edges are equal when their endpoints are equal.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultEdge")]
public class DefaultEdge
{

    /// <summary>
    /// Creates an edge from <paramref name="source"/> (the tail) to <paramref name="target"/> (the head).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultEdge", "DefaultEdge(Object, Object)")]
    public DefaultEdge(object source, object target)
    {
        Source = source;
        Target = target;
    }

    /// <summary>
    /// The tail of the edge.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultEdge", "source")]
    public readonly object Source;

    /// <summary>
    /// The head of the edge.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultEdge", "target")]
    public readonly object Target;

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultEdge", "hashCode()")]
    public override int GetHashCode()
    {
        return Source.GetHashCode() * 31 + Target.GetHashCode();
    }

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultEdge", "equals(Object)")]
    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj)
            || (obj is DefaultEdge other && other.Source.Equals(Source) && other.Target.Equals(Target));
    }

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultEdge", "toString()")]
    public override string ToString()
    {
        return Source + " -> " + Target;
    }

    /// <summary>
    /// A factory that makes plain <see cref="DefaultEdge"/>s.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.graph.DefaultEdge", "factory()")]
    public static DirectedGraph<V, DefaultEdge>.EdgeFactory Factory<V>()
    {
        return new DefaultEdgeFactory<V>();
    }

    /// <summary>
    /// The <see cref="DirectedGraph{V, E}.EdgeFactory"/> returned by <see cref="Factory{V}"/>.
    /// </summary>
    sealed class DefaultEdgeFactory<V> : DirectedGraph<V, DefaultEdge>.EdgeFactory
    {

        /// <inheritdoc/>
        public DefaultEdge CreateEdge(V v0, V v1)
        {
            return new DefaultEdge(v0!, v1!);
        }

    }

}
