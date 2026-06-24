namespace Alembic.Algebra.Metadata;

/// <summary>
/// Placeholder values stored in the metadata cache: <see cref="Instance"/> stands in for a computed
/// <c>null</c> result, and <see cref="Active"/> marks a request that is already in progress (used to
/// detect a cyclic metadata request).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.NullSentinel")]
public sealed class NullSentinel
{
    /// <summary>Placeholder for a <c>null</c> value.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.NullSentinel", "INSTANCE")]
    public static readonly NullSentinel Instance = new NullSentinel("NULL");

    /// <summary>Placeholder that means a request for this metadata is already active (a cycle).</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.NullSentinel", "ACTIVE")]
    public static readonly NullSentinel Active = new NullSentinel("ACTIVE");

    readonly string _name;

    NullSentinel(string name) => _name = name;

    public override string ToString() => _name;

    /// <summary>Returns <paramref name="value"/>, or <see cref="Instance"/> if it is <c>null</c>.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.NullSentinel", "mask(Object)")]
    public static object Mask(object? value) => value ?? Instance;
}
