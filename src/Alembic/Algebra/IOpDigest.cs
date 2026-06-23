namespace Alembic.Algebra;

/// <summary>
/// An op's structural digest — the value the planner compares and hashes ops by, since an op's own
/// <c>Equals</c>/<c>GetHashCode</c> stay as reference identity. Two digests are equal exactly when
/// their ops are <see cref="IOp.DeepEquals"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest")]
public interface IOpDigest
{

    /// <summary>
    /// The op this digest represents.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest", "getRel()")]
    IOp Op { get; }

    /// <summary>
    /// Resets any cached state (e.g. the cached hash), so it is recomputed on next use.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelDigest", "clear()")]
    void Clear();

}
