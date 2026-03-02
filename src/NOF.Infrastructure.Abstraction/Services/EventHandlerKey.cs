namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Strongly-typed marker used as the first element of a composite keyed-service key
/// for event handler registrations. The composite key is
/// <c>(EventHandlerKey.Instance, typeof(TEvent))</c>, which provides logical isolation
/// in the root DI container — consumers without the key cannot accidentally resolve handlers.
/// </summary>
public sealed class EventHandlerKey
{
    /// <summary>Singleton instance used in all event handler keyed registrations.</summary>
    public static readonly EventHandlerKey Instance = new();

    private EventHandlerKey() { }

    /// <summary>
    /// Creates the composite service key for the given event type.
    /// </summary>
    public static (EventHandlerKey, Type) Of(Type eventType) => (Instance, eventType);
}
