using System.Collections.Concurrent;

namespace NOF;

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
}

/// <summary>
/// A thread-safe, keyed event dispatcher supporting both synchronous and asynchronous event handlers.
/// When the key is <see langword="null"/>, it uses a default event bus.
/// </summary>
public sealed class StartupEventChannel : IStartupEventChannel
{
    private static readonly object NullKey = new();

    // Key -> EventType -> List of handlers (stored as object: Action<T> or Func<T, Task>)
    private readonly ConcurrentDictionary<object, ConcurrentDictionary<Type, ConcurrentBag<object>>> _handlers = new();

    /// <inheritdoc />
    public void Subscribe<TEvent>(Action<TEvent> handler, object? key) where TEvent : class
    {
        RegisterHandler(key, typeof(TEvent), handler);
    }

    /// <inheritdoc />
    public void SubscribeAsync<TEvent>(Func<TEvent, Task> handler, object? key) where TEvent : class
    {
        RegisterHandler(key, typeof(TEvent), handler);
    }

    private void RegisterHandler(object? key, Type eventType, object handler)
    {
        key ??= NullKey;
        ArgumentNullException.ThrowIfNull(handler);

        var handlersForKey = _handlers.GetOrAdd(key, _ => new ConcurrentDictionary<Type, ConcurrentBag<object>>());
        var handlersForEvent = handlersForKey.GetOrAdd(eventType, _ => []);
        handlersForEvent.Add(handler);
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, object? key) where TEvent : class
    {
        key ??= NullKey;
        ArgumentNullException.ThrowIfNull(@event);

        if (!_handlers.TryGetValue(key, out var handlersForKey))
            return;

        if (!handlersForKey.TryGetValue(typeof(TEvent), out var handlers))
            return;

        var exceptions = new List<Exception>();
        var snapshot = handlers.ToArray(); // Avoid concurrent modification

        var syncTasks = new List<Task>();

        foreach (var handler in snapshot)
        {
            try
            {
                switch (handler)
                {
                    case Action<TEvent> syncHandler:
                        syncHandler(@event);
                        break;

                    case Func<TEvent, Task> asyncHandler:
                        syncTasks.Add(InvokeAsyncHandler(asyncHandler, @event, exceptions));
                        break;
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (syncTasks.Count > 0)
        {
            await Task.WhenAll(syncTasks).ConfigureAwait(false);
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException("One or more event handlers failed.", exceptions);
        }
    }

    private static async Task InvokeAsyncHandler<TEvent>(
        Func<TEvent, Task> handler,
        TEvent @event,
        List<Exception> exceptions) where TEvent : class
    {
        try
        {
            await handler(@event).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (exceptions)
            {
                exceptions.Add(ex);
            }
        }
    }

    /// <inheritdoc />
    public void UnsubscribeAll<TEvent>(object? key) where TEvent : class
    {
        key ??= NullKey;
        _handlers.TryGetValue(key, out var handlersForKey);
        handlersForKey?.TryRemove(typeof(TEvent), out _);
    }

    /// <inheritdoc />
    public void Clear(object? key)
    {
        key ??= NullKey;
        _handlers.TryRemove(key, out _);
    }

    /// <inheritdoc />
    public int GetHandlerCount<TEvent>(object? key) where TEvent : class
    {
        key ??= NullKey;
        return _handlers.TryGetValue(key, out var handlersForKey)
               && handlersForKey.TryGetValue(typeof(TEvent), out var handlers)
            ? handlers.Count
            : 0;
    }
}
