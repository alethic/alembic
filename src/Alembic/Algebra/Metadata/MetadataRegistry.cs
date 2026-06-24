using System;
using System.Collections.Generic;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// An explicit, per-op-type registry of one metadata kind's implementations. A handler registers an
/// implementation per op type (plus an optional fallback); <see cref="Resolve"/> looks one up by the
/// op's exact runtime type.
/// </summary>
/// <remarks>
/// This is Alembic's stand-in for Calcite's dispatch machinery — <c>ReflectiveRelMetadataProvider</c>
/// (which discovers <c>getXxx(SpecificType, …)</c> handler methods by reflection) and
/// <c>JaninoRelMetadataProvider</c> (which compiles a type-switch dispatcher at runtime). Instead,
/// handlers register their per-type implementations explicitly, so there is no reflection and no
/// runtime code generation. (No <see cref="ProvenanceAttribute"/>: it ports no Calcite member, it
/// replaces a mechanism.)
/// </remarks>
public sealed class MetadataRegistry<TImpl> where TImpl : Delegate
{
    readonly Dictionary<Type, TImpl> _byType = new Dictionary<Type, TImpl>();
    TImpl? _fallback;

    /// <summary>Registers the implementation for ops of exactly <paramref name="opType"/>.</summary>
    public void Register(Type opType, TImpl impl) => _byType[opType] = impl;

    /// <summary>Registers the implementation used for any op type without a specific entry.</summary>
    public void RegisterDefault(TImpl impl) => _fallback = impl;

    /// <summary>
    /// The implementation registered for <paramref name="op"/>'s exact type, else the fallback, else
    /// throws <see cref="NoHandlerException"/>.
    /// </summary>
    public TImpl Resolve(IOp op)
        => _byType.TryGetValue(op.GetType(), out var impl)
            ? impl
            : _fallback ?? throw new NoHandlerException(op.GetType());
}
