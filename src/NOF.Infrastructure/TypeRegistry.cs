using NOF.Abstraction;
using System.Collections.Concurrent;

namespace NOF.Infrastructure;

public static class TypeRegistry
{
    private static readonly ConcurrentDictionary<string, Type> _types = [];

    public static string Register(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var typeName = type.DisplayName;

        _types.TryAdd(typeName, type);
        return typeName;
    }

    public static Type Resolve(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        return _types.TryGetValue(typeName, out var type)
            ? type
            : throw new InvalidOperationException($"type '{typeName}' is not registered.");
    }
}
