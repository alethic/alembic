namespace Alembic.Algebra;

/// <summary>
/// The default <see cref="IOpDigest"/> for an op that does not keep its own: it delegates equality
/// and hashing to the op's <see cref="IOp.DeepEquals"/> / <see cref="IOp.DeepHashCode"/>, caching
/// the hash. <see cref="AbstractOp"/> keeps a richer one that can render the digest string.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest")]
public sealed class OpDigest : IOpDigest
{

    int _hash;

    /// <summary>
    /// Creates a digest for the given op.
    /// </summary>
    public OpDigest(IOp op)
    {
        Op = op;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest", "getRel()")]
    public IOp Op { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest", "clear()")]
    public void Clear() => _hash = 0;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest", "equals(Object)")]
    public override bool Equals(object? obj)
    {
        return obj is IOpDigest other && Op.DeepEquals(other.Op);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest", "hashCode()")]
    public override int GetHashCode()
    {
        if (_hash == 0)
        {
            _hash = Op.DeepHashCode();
            if (_hash == 0) _hash = 1;
        }

        return _hash;
    }

}
