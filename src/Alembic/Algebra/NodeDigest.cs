namespace Alembic.Algebra;

/// <summary>
/// The default <see cref="INodeDigest"/> for a node that does not keep its own: it delegates equality
/// and hashing to the node's <see cref="INode.DeepEquals"/> / <see cref="INode.DeepHashCode"/>, caching
/// the hash. <see cref="AbstractNode"/> keeps a richer one that can render the digest string.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest")]
public sealed class NodeDigest : INodeDigest
{

    int _hash;

    /// <summary>
    /// Creates a digest for the given node.
    /// </summary>
    public NodeDigest(INode node)
    {
        Node = node;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest", "getRel()")]
    public INode Node { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest", "clear()")]
    public void Clear() => _hash = 0;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest", "equals(Object)")]
    public override bool Equals(object? obj)
    {
        return obj is INodeDigest other && Node.DeepEquals(other.Node);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest", "hashCode()")]
    public override int GetHashCode()
    {
        if (_hash == 0)
        {
            _hash = Node.DeepHashCode();
            if (_hash == 0) _hash = 1;
        }

        return _hash;
    }

}
