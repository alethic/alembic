using System;

namespace Alembic;

/// <summary>
/// Records that the annotated type or member derives from a specific class (and optionally member) in
/// the upstream reference implementation this project is a port of. The derivation is kept as structured,
/// reflection-readable metadata — queryable by tooling — rather than in prose comments.
/// </summary>
/// <remarks>
/// Place it on the Alembic type/member and name the upstream class in <see cref="ClassName"/> (and the
/// upstream member, when the derivation is method-level, in <see cref="Member"/>). Multiple attributes
/// are allowed when an implementation draws from more than one upstream source.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum
        | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor | AttributeTargets.Field,
    AllowMultiple = true,
    Inherited = false)]
public sealed class ProvenanceAttribute : Attribute
{

    /// <summary>
    /// Records derivation from the given upstream class, optionally narrowed to a specific member.
    /// </summary>
    public ProvenanceAttribute(string className, string? member = null)
    {
        ClassName = className;
        Member = member;
    }

    /// <summary>
    /// The upstream class this type or member derives from.
    /// </summary>
    public string ClassName { get; }

    /// <summary>
    /// The upstream member this derives from, or <c>null</c> when the derivation is whole-class.
    /// </summary>
    public string? Member { get; }

}
