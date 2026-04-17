using System.Collections.Concurrent;

namespace NOF.Hosting.AspNetCore;

public static class RpcHttpEndpointHandlerRegistry
{
    private readonly record struct Key(Type ServiceType, string MethodName);

    private static readonly ConcurrentDictionary<Key, Entry> _handlers = new();

    public sealed record Entry(Delegate Handler, Type ReturnType);

    public static void Register(Type serviceType, string methodName, Delegate handler, Type returnType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(returnType);

        _handlers[new Key(serviceType, methodName)] = new Entry(handler, returnType);
    }

    public static bool TryGet(Type serviceType, string methodName, out Entry entry)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        return _handlers.TryGetValue(new Key(serviceType, methodName), out entry!);
    }
}

