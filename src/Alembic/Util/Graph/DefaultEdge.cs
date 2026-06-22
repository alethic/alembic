namespace Alembic.Util.Graph;

/// <summary>
/// A directed edge in a <see cref="DirectedGraph{V, E}"/>, from <see cref="Source"/> to
/// <see cref="Target"/>. Two edges are equal when their endpoints are equal.
/// </summary>
public class DefaultEdge
{

    public DefaultEdge(object source, object target)
    {
        Source = source;
        Target = target;
    }

    /// <summary>
    /// The tail of the edge.
    /// </summary>
    public object Source { get; }

    /// <summary>
    /// The head of the edge.
    /// </summary>
    public object Target { get; }

    public override int GetHashCode()
    {
        return Source.GetHashCode() * 31 + Target.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj)
            || (obj is DefaultEdge other && other.Source.Equals(Source) && other.Target.Equals(Target));
    }

    public override string ToString()
    {
        return Source + " -> " + Target;
    }

    /// <summary>
    /// A factory that makes plain <see cref="DefaultEdge"/>s.
    /// </summary>
    public static DirectedGraph<V, DefaultEdge>.EdgeFactory Factory<V>()
    {
        return new DefaultEdgeFactory<V>();
    }

    sealed class DefaultEdgeFactory<V> : DirectedGraph<V, DefaultEdge>.EdgeFactory
    {

        public DefaultEdge CreateEdge(V v0, V v1)
        {
            return new DefaultEdge(v0!, v1!);
        }

    }

}
