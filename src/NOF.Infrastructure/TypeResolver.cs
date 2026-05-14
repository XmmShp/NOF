using NOF.Abstraction;
using System.Collections.Concurrent;

namespace NOF.Infrastructure;

public sealed class TypeResolver
{
    private readonly ConcurrentDictionary<string, Type> _types = new(StringComparer.Ordinal);

    public string Register(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var typeName = type.DisplayName;
        _types.TryAdd(typeName, type);
        return typeName;
    }

    public Type Resolve(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        return _types.TryGetValue(typeName, out var type)
            ? type
            : throw new InvalidOperationException($"type '{typeName}' is not registered.");
    }
}
