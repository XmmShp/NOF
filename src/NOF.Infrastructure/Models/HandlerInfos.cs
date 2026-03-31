using NOF.Contract;
using System.Reflection;

namespace NOF.Infrastructure;

/// <summary>
/// Centralized registry of all handler metadata collected during service registration.
/// Contains typed sets for each handler kind and a single shared endpoint name resolution.
/// Registered as a singleton in DI.
/// </summary>
public sealed class HandlerInfos
{
    public HashSet<CommandHandlerInfo> Commands { get; } = [];
    public HashSet<EventHandlerInfo> Events { get; } = [];
    public HashSet<NotificationHandlerInfo> Notifications { get; } = [];

    /// <summary>
    /// Adds a handler info to the appropriate typed set using pattern matching.
    /// </summary>
    public void Add(HandlerInfo info)
    {
        switch (info)
        {
            case CommandHandlerInfo command:
                Commands.Add(command);
                break;
            case EventHandlerInfo @event:
                Events.Add(@event);
                break;
            case NotificationHandlerInfo notification:
                Notifications.Add(notification);
                break;
            default:
                throw new ArgumentException($"Unknown handler info type: {info.GetType()}", nameof(info));
        }
    }

    /// <summary>
    /// Adds multiple handler infos to the appropriate typed sets.
    /// </summary>
    public void AddRange(ReadOnlySpan<HandlerInfo> infos)
    {
        foreach (var info in infos)
        {
            Add(info);
        }
    }

    private readonly Dictionary<Type, string> _endpointNames = [];
    private readonly HashSet<Type> _noAttr = [];

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
        if (_endpointNames.TryGetValue(handlerType, out name!))
        {
            return true;
        }

        if (_noAttr.Contains(handlerType))
        {
            return false;
        }

        var attr = handlerType.GetCustomAttribute<EndpointNameAttribute>();
        if (attr is not null)
        {
            _endpointNames[handlerType] = name = attr.Name;
            return true;
        }
        _noAttr.Add(handlerType);
        return false;
    }
}
