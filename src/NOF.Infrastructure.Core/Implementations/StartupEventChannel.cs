using System.Collections.Concurrent;

namespace NOF;

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
