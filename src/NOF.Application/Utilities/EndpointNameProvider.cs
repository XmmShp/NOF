using System.Collections.Concurrent;
using System.Reflection;

namespace NOF.Application.Utilities;

public class EndpointNameProvider
{
    public static EndpointNameProvider Instance { get; protected set; } = new();
    private static readonly ConcurrentDictionary<Type, string> NameCache = [];
    public virtual string GetEndpointName(Type type)
    {
        if (NameCache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        if (type.GetCustomAttribute<EndpointNameAttribute>() is { } attr)
        {
            return NameCache.GetOrAdd(type, attr.Name);
        }

        var messageTypes = type.GetInterfaces()
            .Where(iface =>
            {
                if (!iface.IsGenericType)
                {
                    return false;
                }

                var def = iface.GetGenericTypeDefinition();

                return def == typeof(ICommandHandler<>) ||
                       def == typeof(IRequestHandler<>) ||
                       def == typeof(IRequestHandler<,>);
            })
            .Select(iface => iface.GetGenericArguments()[0]).ToList();

        if (messageTypes.Count > 1)
        {
            var interfaceNames = string.Join(", ", messageTypes.Select(mt => mt.FullName ?? mt.Name));
            throw new InvalidOperationException($"Type '{type.FullName}' implements multiple handler interfaces with different message types: {interfaceNames}. " +
                                                "Endpoint name cannot be uniquely determined.");
        }

        if (messageTypes.Count == 1)
        {
            var messageType = messageTypes[0];
            var endpointName = GetEndpointName(messageType);
            return NameCache.GetOrAdd(type, endpointName);
        }

        var fallbackName = BuildSafeTypeName(type);
        return NameCache.GetOrAdd(type, fallbackName);
    }

    protected virtual string BuildSafeTypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return (type.FullName ?? type.Name).Replace('.', '_').Replace("+", "____");
        }

        var genericDef = type.GetGenericTypeDefinition();
        var defName = (genericDef.FullName?[..genericDef.FullName.LastIndexOf('`')] ?? genericDef.Name).Replace('.', '_').Replace("+", "____");

        var args = type.GetGenericArguments()
            .Select(BuildSafeTypeName)
            .ToArray();

        return $"{defName}__{string.Join("___", args)}";
    }
}