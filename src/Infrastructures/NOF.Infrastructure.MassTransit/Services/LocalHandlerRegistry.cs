namespace NOF.Infrastructure.MassTransit;

/// <summary>
/// Singleton registry of message types that have locally registered handlers,
/// along with the endpoint name resolved from the handler type at startup.
/// Populated during <see cref="MassTransitRegistrationStep"/>.
/// Used by riders to decide whether to dispatch locally (mediator) or remotely (bus).
/// </summary>
public sealed class LocalHandlerRegistry
{
    private readonly Dictionary<Type, HashSet<string>> _entries = new();

    /// <summary>
    /// Registers a message type with its local endpoint name (resolved from the handler type).
    /// A single message type may have multiple handlers with different endpoint names.
    /// </summary>
    public void Register(Type messageType, string endpointName)
    {
        if (!_entries.TryGetValue(messageType, out var names))
        {
            names = [];
            _entries[messageType] = names;
        }

        names.Add(endpointName);
    }

    /// <summary>
    /// Determines whether a message should be dispatched locally via mediator.
    /// <list type="bullet">
    ///   <item>If <paramref name="destinationEndpointName"/> is null/whitespace and a local handler is registered → local.</item>
    ///   <item>If <paramref name="destinationEndpointName"/> matches any local endpoint name for this message type (case-sensitive) → local.</item>
    ///   <item>Otherwise → remote.</item>
    /// </list>
    /// </summary>
    public bool ShouldDispatchLocally(Type messageType, string? destinationEndpointName)
    {
        if (string.IsNullOrWhiteSpace(destinationEndpointName))
        {
            return _entries.ContainsKey(messageType);
        }

        if (!_entries.TryGetValue(messageType, out var localEndpointNames))
        {
            return false;
        }

        return localEndpointNames.Contains(destinationEndpointName);
    }
}
