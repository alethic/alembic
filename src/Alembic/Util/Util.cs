namespace Alembic.Util;

/// <summary>
/// Miscellaneous utility functions.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Util")]
public static class Util
{

    /// <summary>
    /// Returns the first value if it is not null, otherwise the second value.
    /// </summary>
    /// <remarks>
    /// The result may be null only if the second argument is not null. Equivalent to the Elvis operator
    /// (<c>?:</c>) of languages such as Groovy or PHP.
    /// </remarks>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Util", "first(T, T)")]
    public static T? First<T>(T? v0, T? v1)
        where T : class
        => v0 is not null ? v0 : v1;

}
