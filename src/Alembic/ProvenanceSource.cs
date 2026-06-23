namespace Alembic;

/// <summary>
/// Where an annotated type or member originates.
/// </summary>
public enum ProvenanceSource
{

    /// <summary>
    /// Alembic-original, reviewed and approved as such: no upstream analog by design. This value is
    /// applied only by deliberate human decision — never inferred or added automatically.
    /// </summary>
    Local,

    /// <summary>
    /// Derived from Apache Calcite (the overwhelmingly common source).
    /// </summary>
    Calcite,

    /// <summary>
    /// Derived from some other upstream project (e.g. Google Guava).
    /// </summary>
    Other,

}
