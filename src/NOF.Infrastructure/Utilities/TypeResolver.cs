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

        if (_types.TryGetValue(typeName, out var type))
        {
            return type;
        }

        type = Type.GetType(typeName, throwOnError: false)
            ?? AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                .FirstOrDefault(static resolvedType => resolvedType is not null);
        if (type is not null)
        {
            Register(type);
            _types.TryAdd(typeName, type);
            return type;
        }

        throw new InvalidOperationException($"type '{typeName}' is not registered.");
    }
}
