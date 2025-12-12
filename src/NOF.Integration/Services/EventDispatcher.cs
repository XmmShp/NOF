using System.Collections.Concurrent;

namespace NOF;

/// <summary>
/// A thread-safe event dispatcher that supports keyed isolation.
/// When key is <see langword="null"/>, it uses a default event bus.
/// </summary>
public static class EventDispatcher
{
    private static readonly object NullKey = new();

    // Key -> EventType -> Handlers
    private static readonly ConcurrentDictionary<object, ConcurrentDictionary<Type, ConcurrentBag<object>>> Handlers = new();

    /// <summary>
    /// Subscribes a synchronous handler for the specified event type under the given key.
    /// If <paramref name="key"/> is <see langword="null"/>, the handler is registered to default scope.
    /// </summary>
    /// <typeparam name="TEvent">The event type (must be a reference type).</typeparam>
    /// <param name="handler">The handler to register.</param>
    /// <param name="key">Optional key to isolate the event bus. Use <see langword="null"/> for default scope.</param>
    public static void Subscribe<TEvent>(Action<TEvent> handler, object? key = null)
    {
        key ??= NullKey;
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);
        var handlersForKey = Handlers.GetOrAdd(key, _ => new ConcurrentDictionary<Type, ConcurrentBag<object>>());
        var handlersForEvent = handlersForKey.GetOrAdd(eventType, _ => []);
        handlersForEvent.Add(handler);
    }

    /// <summary>
    /// Publishes an event to all handlers registered for the given key and event type.
    /// If <paramref name="key"/> is <see langword="null"/>, publishes to the default event bus.
    /// </summary>
    /// <typeparam name="TEvent">The event type (must be a reference type).</typeparam>
    /// <param name="event">The event instance to publish.</param>
    /// <param name="key">Optional key to target a specific event bus. Use <see langword="null"/> for default scope.</param>
    public static void Publish<TEvent>(TEvent @event, object? key = null)
    {
        key ??= NullKey;
        ArgumentNullException.ThrowIfNull(@event);

        if (!Handlers.TryGetValue(key, out var handlersForKey))
            return;

        if (!handlersForKey.TryGetValue(typeof(TEvent), out var handlers))
            return;

        var exceptions = new List<Exception>();
        var snapshot = handlers.ToArray();

        foreach (var handler in snapshot)
        {
            try
            {
                if (handler is Action<TEvent> typedHandler)
                {
                    typedHandler(@event);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException("One or more event handlers failed.", exceptions);
        }
    }

    /// <summary>
    /// Unsubscribes all handlers for the specified event type under the given key.
    /// If <paramref name="key"/> is <see langword="null"/>, clears default handlers for this event.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="key">The key of the event bus to clear. Use <see langword="null"/> for default scope.</param>
    public static void UnsubscribeAll<TEvent>(object? key = null)
    {
        key ??= NullKey;
        if (Handlers.TryGetValue(key, out var handlersForKey))
        {
            handlersForKey.TryRemove(typeof(TEvent), out _);
        }
    }

    /// <summary>
    /// Clears all handlers for the given key (entire event bus reset).
    /// If <paramref name="key"/> is <see langword="null"/>, clears the default event bus.
    /// </summary>
    /// <param name="key">The key to clear. Use <see langword="null"/> for default scope.</param>
    public static void Clear(object? key = null)
    {
        key ??= NullKey;
        Handlers.TryRemove(key, out _);
    }

    /// <summary>
    /// Gets the number of handlers registered for the specified event type under the given key.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="key">The key of the event bus. Use <see langword="null"/> for default scope.</param>
    /// <returns>The number of registered handlers.</returns>
    public static int GetHandlerCount<TEvent>(object? key = null)
    {
        key ??= NullKey;
        return Handlers.TryGetValue(key, out var handlersForKey)
               && handlersForKey.TryGetValue(typeof(TEvent), out var handlers)
            ? handlers.Count
            : 0;
    }
}