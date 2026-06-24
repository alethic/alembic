using System;
using System.Collections.Generic;
using System.Collections.Immutable;

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
    public static IContext Chain(params IContext[] contexts) => Chain((IEnumerable<IContext>)contexts);

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

        public WrapContext(object target) => _target = target ?? throw new ArgumentNullException(nameof(target));

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.WrapContext", "unwrap(Class)")]
        public C? Unwrap<C>() where C : class => _target as C;

    }

    /// <summary>A context that recognises nothing.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.EmptyContext")]
    sealed class EmptyContext : IContext
    {

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.EmptyContext", "unwrap(Class)")]
        public C? Unwrap<C>() where C : class => null;

    }

    /// <summary>A context that wraps a (flat) chain of contexts.</summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.ChainContext")]
    sealed class ChainContext : IContext
    {

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.ChainContext", "contexts")]
        internal readonly ImmutableArray<IContext> Contexts;

        public ChainContext(ImmutableArray<IContext> contexts) => Contexts = contexts;

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Contexts.ChainContext", "unwrap(Class)")]
        public C? Unwrap<C>() where C : class
        {
            foreach (var context in Contexts)
            {
                var t = context.Unwrap<C>();
                if (t is not null)
                    return t;
            }

            return null;
        }

    }

}
