using System;

namespace Alembic.Util;

/// <summary>
/// Mix-in interface that lets you find a sub-object an implementer carries. The .NET analog of Calcite's
/// generic <c>Wrapper</c> (its <c>Class&lt;C&gt;</c> argument becomes a type parameter). Works for value
/// types too: an absent value type yields <c>default</c> (e.g. <c>CancellationToken.None</c>).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.schema.Wrapper")]
public interface IWrapper
{

    /// <summary>
    /// Finds an instance of <typeparamref name="C"/> carried by this object, or its default if there is
    /// none (<c>null</c> for a reference type, <c>default</c> for a value type).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.schema.Wrapper", "unwrap(Class)")]
    C? Unwrap<C>();

    /// <summary>
    /// Finds an instance of the reference type <typeparamref name="C"/>, or throws if there is none.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.schema.Wrapper", "unwrapOrThrow(Class)")]
    C UnwrapOrThrow<C>() where C : class
        => Unwrap<C>() ?? throw new InvalidOperationException($"Can't unwrap {typeof(C)} from {this}");

    /// <summary>
    /// Finds an instance of <typeparamref name="C"/>, or its default — the .NET analog of Calcite's
    /// <c>Optional</c>-returning <c>maybeUnwrap</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.schema.Wrapper", "maybeUnwrap(Class)")]
    C? MaybeUnwrap<C>() => Unwrap<C>();

}
