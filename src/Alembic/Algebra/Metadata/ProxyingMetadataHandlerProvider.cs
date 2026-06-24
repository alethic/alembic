using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Alembic.Algebra.Metadata;

/// <summary>
/// Produces a handler dispatcher by reflection: for a handler interface, it reflects over the handler
/// objects' typed methods, maps each to the op type it handles, and returns a proxy implementing the
/// interface that routes every call to the most-specific method for the op at hand.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ProxyingMetadataHandlerProvider")]
public sealed class ProxyingMetadataHandlerProvider : IMetadataHandlerProvider
{
    readonly IOpMetadataProvider _provider;

    /// <summary>
    /// Creates a provider that reflects over the handlers supplied by <paramref name="provider"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ProxyingMetadataHandlerProvider", "ProxyingMetadataHandlerProvider(RelMetadataProvider)")]
    public ProxyingMetadataHandlerProvider(IOpMetadataProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// Builds and returns a dispatcher implementing the handler interface <typeparamref name="THandler"/>,
    /// routing each call to the most-specific registered handler method for the op at hand.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ProxyingMetadataHandlerProvider", "handler(Class)")]
    public THandler Handler<THandler>() where THandler : class
    {
        var handlerClass = typeof(THandler);
        var interfaceMethods = handlerClass.GetMethods();

        // Build, by reflection, (interface-method name, op type) -> the handler method that implements it.
        var map = new Dictionary<(string, Type), (object Target, MethodInfo Method)>();
        foreach (var target in _provider.Handlers(handlerClass))
            foreach (var handlerMethod in target.GetType().GetMethods())
                foreach (var interfaceMethod in interfaceMethods)
                    if (CouldImplement(handlerMethod, interfaceMethod))
                        map[(interfaceMethod.Name, handlerMethod.GetParameters()[0].ParameterType)] = (target, handlerMethod);

        var proxy = DispatchProxy.Create<THandler, HandlerDispatchProxy>();
        ((HandlerDispatchProxy)(object)proxy).Init(map);
        return proxy;
    }

    /// <summary>
    /// Whether <paramref name="handlerMethod"/> implements <paramref name="interfaceMethod"/>: same name,
    /// and the same parameters but with the first being any op type and the second the metadata query.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ReflectiveRelMetadataProvider", "couldImplement(Method, Method)")]
    static bool CouldImplement(MethodInfo handlerMethod, MethodInfo interfaceMethod)
    {
        if (handlerMethod.Name != interfaceMethod.Name || handlerMethod.IsStatic || !handlerMethod.IsPublic)
            return false;

        var handlerTypes = handlerMethod.GetParameters();
        var interfaceTypes = interfaceMethod.GetParameters();
        if (handlerTypes.Length != interfaceTypes.Length || handlerTypes.Length < 2)
            return false;

        if (!typeof(IOp).IsAssignableFrom(handlerTypes[0].ParameterType))
            return false;

        if (handlerTypes[1].ParameterType != typeof(OpMetadataQuery))
            return false;

        for (int i = 2; i < handlerTypes.Length; i++)
            if (handlerTypes[i].ParameterType != interfaceTypes[i].ParameterType)
                return false;

        return true;
    }
}

/// <summary>
/// The proxy that <see cref="ProxyingMetadataHandlerProvider"/> returns: on each handler-interface call,
/// it resolves the most-specific handler method for the bound op's runtime type and invokes it.
/// </summary>
public class HandlerDispatchProxy : DispatchProxy
{
    Dictionary<(string, Type), (object Target, MethodInfo Method)> _map = null!;

    /// <summary>
    /// Installs the (interface-method name, op type) → handler-method dispatch map.
    /// </summary>
    internal void Init(Dictionary<(string, Type), (object Target, MethodInfo Method)> map) => _map = map;

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        // Only the metadata methods (op first, then the query) are dispatched; anything else on the
        // handler interface (e.g. getDef) is not supported on the proxy, as in Calcite.
        if (args is null || args.Length == 0 || args[0] is not IOp op)
            throw new NotSupportedException($"Not supported: {targetMethod?.Name}");

        var (target, method) = Resolve(targetMethod!.Name, op.GetType());
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(e.InnerException);
            throw; // unreachable
        }
    }

    /// <summary>
    /// Finds the method registered for the most-specific supertype of <paramref name="opType"/>: its own
    /// type, then its base classes, then its interfaces (the catch-all is registered against IOp).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.metadata.ReflectiveRelMetadataProvider.Space", "find(Class, Method)")]
    (object Target, MethodInfo Method) Resolve(string name, Type opType)
    {
        for (var t = opType; t is not null; t = t.BaseType)
            if (_map.TryGetValue((name, t), out var found))
                return found;

        foreach (var i in opType.GetInterfaces())
            if (_map.TryGetValue((name, i), out var found))
                return found;

        throw new NoHandlerException(opType);
    }
}
