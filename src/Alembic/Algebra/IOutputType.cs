namespace Alembic.Algebra;

/// <summary>
/// Describes the output an <see cref="IOp"/> produces. Alembic attaches no meaning to it; the user
/// supplies the concrete types. The planner treats it opaquely and asks only one thing of it: whether
/// two ops' outputs are equivalent — the invariant every op in an equivalence set shares.
/// </summary>
/// <remarks>
/// This is a pure equivalence relation, not a partial order: an output type has no notion of
/// converting to another (unlike a trait, which an enforcer can convert within a set). To change an
/// op's output type, a rule rewrites it into a different op — output types partition sets, they never
/// convert within one.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.type.RelDataType")]
public interface IOutputType
{

    /// <summary>
    /// Whether this output type is equivalent to <paramref name="other"/> — the comparison the planner
    /// uses to enforce that an equivalence set holds only ops of the same output (and to deduplicate
    /// ops). Equivalence ignores any purely cosmetic naming, as Calcite's
    /// <c>equalsSansFieldNames</c> / <c>areRowTypesEqual(_, _, false)</c> does.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.type.RelDataType", "equalsSansFieldNames(RelDataType)")]
    bool IsEquivalentTo(IOutputType other);

}
