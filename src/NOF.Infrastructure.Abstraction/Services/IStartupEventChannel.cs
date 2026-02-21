namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Defines a thread-safe, keyed event dispatcher that supports isolated event buses
/// and both synchronous and asynchronous event handlers.
/// </summary>
public interface IStartupEventChannel
{
    /// <summary>
    /// Subscribes a synchronous handler for the specified event type under the given key.
    /// If <paramref name="key"/> is <see langword="null"/>, the handler is registered to the default scope.
    /// </summary>
    /// <typeparam name="TEvent">The event type (must be a reference type).</typeparam>
    /// <param name="handler">The synchronous handler to register.</param>
    /// <param name="key">Optional key to isolate the event bus. Use <see langword="null"/> for default scope.</param>
    void Subscribe<TEvent>(Action<TEvent> handler, object? key = null) where TEvent : class;

    /// <summary>
    /// Subscribes an asynchronous handler for the specified event type under the given key.
    /// If <paramref name="key"/> is <see langword="null"/>, the handler is registered to the default scope.
    /// </summary>
    /// <typeparam name="TEvent">The event type (must be a reference type).</typeparam>
    /// <param name="handler">The asynchronous handler to register.</param>
    /// <param name="key">Optional key to isolate the event bus. Use <see langword="null"/> for default scope.</param>
    void SubscribeAsync<TEvent>(Func<TEvent, Task> handler, object? key = null) where TEvent : class;

    /// <summary>
    /// Publishes an event to all registered synchronous and asynchronous handlers for the given key and event type.
    /// Handlers are invoked in parallel (fire-and-forget for sync, awaited for async).
    /// If <paramref name="key"/> is <see langword="null"/>, publishes to the default event bus.
    /// Any exceptions from handlers are collected and thrown as an <see cref="AggregateException"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event instance to publish.</param>
    /// <param name="key">Optional key to target a specific event bus. Use <see langword="null"/> for default scope.</param>
    /// <returns>A task that completes when all handlers have finished processing.</returns>
    Task PublishAsync<TEvent>(TEvent @event, object? key = null) where TEvent : class;

    /// <summary>
    /// Unsubscribes all handlers (both sync and async) for the specified event type under the given key.
    /// If <paramref name="key"/> is <see langword="null"/>, clears default handlers for this event.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="key">The key of the event bus to clear. Use <see langword="null"/> for default scope.</param>
    void UnsubscribeAll<TEvent>(object? key = null) where TEvent : class;

    /// <summary>
    /// Clears all handlers for the given key (entire event bus reset).
    /// If <paramref name="key"/> is <see langword="null"/>, clears the default event bus.
    /// </summary>
    /// <param name="key">The key to clear. Use <see langword="null"/> for default scope.</param>
    void Clear(object? key = null);

    /// <summary>
    /// Gets the total number of handlers (sync + async) registered for the specified event type under the given key.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="key">The key of the event bus. Use <see langword="null"/> for default scope.</param>
    /// <returns>The number of registered handlers.</returns>
    int GetHandlerCount<TEvent>(object? key = null) where TEvent : class;

    void Publish<TEvent>(TEvent @event, object? key = null) where TEvent : class
    {
        PublishAsync(@event, key).GetAwaiter().GetResult();
    }
}
