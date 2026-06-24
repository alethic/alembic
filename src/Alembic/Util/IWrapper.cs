using System;

namespace Alembic.Util;

/// <summary>
/// Mix-in interface that lets you find a sub-object an implementer carries. The .NET analog of Calcite's
/// generic <c>Wrapper</c> (its <c>Class&lt;C&gt;</c> argument becomes a type parameter, and its
/// <c>@Nullable C</c> / <c>Optional&lt;C&gt;</c> results become a nullable reference).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.schema.Wrapper")]
public interface IWrapper
{

    /// <summary>
    /// Finds an instance of <typeparamref name="C"/> carried by this object, or <c>null</c> if there is
    /// none.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.schema.Wrapper", "unwrap(Class)")]
    C? Unwrap<C>() where C : class;

    /// <summary>
    /// Finds an instance of <typeparamref name="C"/>, or throws if there is none.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.schema.Wrapper", "unwrapOrThrow(Class)")]
    C UnwrapOrThrow<C>() where C : class
        => Unwrap<C>() ?? throw new InvalidOperationException($"Can't unwrap {typeof(C)} from {this}");

    /// <summary>
    /// Finds an instance of <typeparamref name="C"/>, or <c>null</c> — the nullable-reference analog of
    /// Calcite's <c>Optional</c>-returning <c>maybeUnwrap</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.schema.Wrapper", "maybeUnwrap(Class)")]
    C? MaybeUnwrap<C>() where C : class => Unwrap<C>();

}
