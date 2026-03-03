using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Provides endpoint names from a pre-built <see cref="EndpointNameRegistry"/> dictionary.
/// Names are typically computed at compile time by the source generator and registered during
/// <c>AddAllHandlers</c>. For types not pre-registered, falls back to a safe type-name derivation.
/// </summary>
public class ManualEndpointNameProvider : IEndpointNameProvider
{
    private readonly EndpointNameRegistry _options;

    public ManualEndpointNameProvider(EndpointNameRegistry options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public virtual string GetEndpointName(Type type)
    {
        if (_options.TryGetValue(type, out var name))
        {
            return name;
        }

        var fallbackName = BuildSafeTypeName(type);
        return _options.GetOrAdd(type, fallbackName);
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
