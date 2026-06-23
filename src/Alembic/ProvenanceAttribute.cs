using System;

namespace Alembic;

/// <summary>
/// Records that the annotated type or member derives from a specific class (and optionally member) in
/// the upstream reference implementation this project is a port of. The derivation is kept as structured,
/// reflection-readable metadata — queryable by tooling — rather than in prose comments.
/// </summary>
/// <remarks>
/// <para>
/// Place it on the Alembic type/member. <see cref="Source"/> — which upstream project the derivation is
/// from — is the required first argument; the upstream class follows in <see cref="ClassName"/> (and the
/// upstream member, when the derivation is method-level, in <see cref="Member"/>). Multiple attributes
/// are allowed when an implementation draws from more than one upstream source.
/// </para>
/// <para>
/// Mark <em>only</em> a type or member that is an exact derivation of a specific upstream type or member:
/// a faithful port that fills the same role and does the same thing, differing only by the renaming and
/// repackaging that the C# port demands. Do <em>not</em> mark something merely because the upstream has a
/// similarly named or related construct. In particular, do not annotate a member that is analogous but
/// not a port, one that decomposes a single upstream member into several (or fuses several into one), or
/// a structural device the port introduces with no upstream counterpart. When in doubt, leave it
/// un-annotated: the absence of the attribute is itself the record that the member is Alembic-original.
/// </para>
/// <para>
/// <see cref="ProvenanceSource.Local"/> is the one exception to "absence means original": it is an
/// explicit, reviewed assertion that a member is intentionally Alembic-original. It is applied only by
/// deliberate human decision and must never be added automatically or inferred.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
public sealed class ProvenanceAttribute : Attribute
{

    /// <summary>
    /// Records derivation from the given upstream class, optionally narrowed to a specific member.
    /// <see cref="Source"/> is the required first argument.
    /// </summary>
    public ProvenanceAttribute(ProvenanceSource source, string className, string? member = null)
    {
        Source = source;
        ClassName = className;
        Member = member;
    }

    /// <summary>
    /// Records origin without an upstream class — used to mark a reviewed
    /// <see cref="ProvenanceSource.Local"/> (Alembic-original) member.
    /// </summary>
    public ProvenanceAttribute(ProvenanceSource source)
    {
        Source = source;
    }

    /// <summary>
    /// Which upstream project the derivation is from — the required first constructor argument.
    /// </summary>
    public ProvenanceSource Source { get; }

    /// <summary>
    /// The upstream class this type or member derives from, or <c>null</c> for a <see cref="ProvenanceSource.Local"/> member.
    /// </summary>
    public string? ClassName { get; }

    /// <summary>
    /// The upstream member this derives from, or <c>null</c> when the derivation is whole-class.
    /// </summary>
    public string? Member { get; }

}
