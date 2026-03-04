using NOF.Contract;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Strongly-typed set of <see cref="CommandHandlerInfo"/> metadata.
/// Registered as a singleton in DI.
/// </summary>
public sealed class CommandHandlerInfos : HashSet<CommandHandlerInfo>
{
    private readonly Dictionary<Type, string> _endpointNames = new();
    private readonly HashSet<Type> _noAttr = new();

    /// <summary>
    /// Gets the endpoint name for a handler type.
    /// If not explicitly set, checks <see cref="EndpointNameAttribute"/> (cached on first miss),
    /// then falls back to <see cref="EndpointNameHelper.BuildSafeTypeName"/>.
    /// </summary>
    public string GetEndpointName(Type handlerType)
        => TryResolve(handlerType, out var name) ? name : EndpointNameHelper.BuildSafeTypeName(handlerType);

    /// <summary>Explicitly overrides the endpoint name for a handler type.</summary>
    public void SetEndpointName(Type handlerType, string endpointName)
        => _endpointNames[handlerType] = endpointName;

    /// <summary>
    /// Returns <c>true</c> if the handler type has an explicitly set endpoint name
    /// (via <see cref="SetEndpointName"/> or <see cref="EndpointNameAttribute"/>).
    /// </summary>
    public bool HasExplicitEndpointName(Type handlerType)
        => TryResolve(handlerType, out _);

    private bool TryResolve(Type handlerType, out string name)
    {
        if (_endpointNames.TryGetValue(handlerType, out name!)) return true;
        if (_noAttr.Contains(handlerType)) return false;
        var attr = (EndpointNameAttribute?)Attribute.GetCustomAttribute(handlerType, typeof(EndpointNameAttribute));
        if (attr is not null) { _endpointNames[handlerType] = name = attr.Name; return true; }
        _noAttr.Add(handlerType);
        return false;
    }
}

/// <summary>
/// Strongly-typed set of <see cref="EventHandlerInfo"/> metadata.
/// Registered as a singleton in DI.
/// </summary>
public sealed class EventHandlerInfos : HashSet<EventHandlerInfo>;

/// <summary>
/// Strongly-typed set of <see cref="NotificationHandlerInfo"/> metadata.
/// Registered as a singleton in DI.
/// </summary>
public sealed class NotificationHandlerInfos : HashSet<NotificationHandlerInfo>;

/// <summary>
/// Strongly-typed set of <see cref="RequestWithoutResponseHandlerInfo"/> metadata.
/// Registered as a singleton in DI.
/// </summary>
public sealed class RequestWithoutResponseHandlerInfos : HashSet<RequestWithoutResponseHandlerInfo>
{
    private readonly Dictionary<Type, string> _endpointNames = new();
    private readonly HashSet<Type> _noAttr = new();

    public string GetEndpointName(Type handlerType)
        => TryResolve(handlerType, out var name) ? name : EndpointNameHelper.BuildSafeTypeName(handlerType);

    public void SetEndpointName(Type handlerType, string endpointName)
        => _endpointNames[handlerType] = endpointName;

    public bool HasExplicitEndpointName(Type handlerType)
        => TryResolve(handlerType, out _);

    private bool TryResolve(Type handlerType, out string name)
    {
        if (_endpointNames.TryGetValue(handlerType, out name!)) return true;
        if (_noAttr.Contains(handlerType)) return false;
        var attr = (EndpointNameAttribute?)Attribute.GetCustomAttribute(handlerType, typeof(EndpointNameAttribute));
        if (attr is not null) { _endpointNames[handlerType] = name = attr.Name; return true; }
        _noAttr.Add(handlerType);
        return false;
    }
}

/// <summary>
/// Strongly-typed set of <see cref="RequestWithResponseHandlerInfo"/> metadata.
/// Registered as a singleton in DI.
/// </summary>
public sealed class RequestWithResponseHandlerInfos : HashSet<RequestWithResponseHandlerInfo>
{
    private readonly Dictionary<Type, string> _endpointNames = new();
    private readonly HashSet<Type> _noAttr = new();

    public string GetEndpointName(Type handlerType)
        => TryResolve(handlerType, out var name) ? name : EndpointNameHelper.BuildSafeTypeName(handlerType);

    public void SetEndpointName(Type handlerType, string endpointName)
        => _endpointNames[handlerType] = endpointName;

    public bool HasExplicitEndpointName(Type handlerType)
        => TryResolve(handlerType, out _);

    private bool TryResolve(Type handlerType, out string name)
    {
        if (_endpointNames.TryGetValue(handlerType, out name!)) return true;
        if (_noAttr.Contains(handlerType)) return false;
        var attr = (EndpointNameAttribute?)Attribute.GetCustomAttribute(handlerType, typeof(EndpointNameAttribute));
        if (attr is not null) { _endpointNames[handlerType] = name = attr.Name; return true; }
        _noAttr.Add(handlerType);
        return false;
    }
}
