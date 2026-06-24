using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Alembic.Plan;

/// <summary>
/// Factory and implementations for <see cref="IContext"/>: an empty context, a context that wraps a
/// single object, and a context that chains several (the first to recognise a request wins).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts")]
public static class Contexts
{

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts", "EMPTY_CONTEXT")]
    static readonly EmptyContext EmptyContextValue = new EmptyContext();

    /// <summary>
    /// A context that recognises nothing.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts", "empty()")]
    public static IContext Empty() => EmptyContextValue;

    /// <summary>
    /// A context that wraps a single object.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts", "of(Object)")]
    public static IContext Of(object o) => new WrapContext(o);

    /// <summary>
    /// A context that wraps several objects (nulls are ignored).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts", "of(Object...)")]
    public static IContext Of(params object?[] os)
    {
        var contexts = new List<IContext>();
        foreach (var o in os)
            if (o is not null)
                contexts.Add(Of(o));

        return Chain(contexts);
    }

    /// <summary>
    /// A context that chains the given contexts.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts", "chain(Context...)")]
    public static IContext Chain(params IContext[] contexts) => Chain(contexts.ToImmutableArray());

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts", "chain(Iterable)")]
    static IContext Chain(IEnumerable<IContext> contexts)
    {
        var list = new List<IContext>();
        foreach (var context in contexts)
            Build(list, context);

        return list.Count switch
        {
            0 => Empty(),
            1 => list[0],
            _ => new ChainContext(list.ToImmutableArray()),
        };
    }

    /// <summary>Recursively flattens contexts into <paramref name="list"/>.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts", "build(List, Context)")]
    static void Build(List<IContext> list, IContext context)
    {
        if (ReferenceEquals(context, EmptyContextValue) || list.Contains(context))
            return;

        if (context is ChainContext chain)
        {
            foreach (var child in chain.Contexts)
                Build(list, child);
        }
        else
        {
            list.Add(context);
        }
    }

    /// <summary>A context that wraps a single object.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.WrapContext")]
    sealed class WrapContext : IContext
    {

        readonly object _target;

        /// <summary>
        /// Wraps <paramref name="target"/>.
        /// </summary>
        public WrapContext(object target) => _target = target ?? throw new ArgumentNullException(nameof(target));

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.WrapContext", "unwrap(Class)")]
        public C? Unwrap<C>() => _target is C c ? c : default;

    }

    /// <summary>A context that recognises nothing.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.EmptyContext")]
    sealed class EmptyContext : IContext
    {

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.EmptyContext", "unwrap(Class)")]
        public C? Unwrap<C>() => default;

    }

    /// <summary>A context that wraps a (flat) chain of contexts.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.ChainContext")]
    sealed class ChainContext : IContext
    {

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.ChainContext", "contexts")]
        internal readonly ImmutableArray<IContext> Contexts;

        /// <summary>
        /// Wraps the flat chain <paramref name="contexts"/>.
        /// </summary>
        public ChainContext(ImmutableArray<IContext> contexts)
        {
            Contexts = contexts;
            foreach (var context in contexts)
                Debug.Assert(context is not ChainContext, "must be flat");
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.ChainContext", "unwrap(Class)")]
        public C? Unwrap<C>()
        {
            foreach (var context in Contexts)
            {
                var t = context.Unwrap<C>();
                if (t is not null)
                    return t;
            }

            return default;
        }

    }

}
