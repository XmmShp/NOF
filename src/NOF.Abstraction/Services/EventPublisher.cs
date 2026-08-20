using NOF.Contract;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

/// <summary>
/// Provides convenience access to the ambient <see cref="IEventPublisher"/> for the current async flow.
/// </summary>
/// <remarks>
/// Prefer explicit <see cref="IEventPublisher"/> dependencies in core runtime paths.
/// The ambient publisher exists as a convenience API for in-scope code that wants a lighter call site.
/// </remarks>
public static class EventPublisher
{
    private static readonly AsyncLocal<IEventPublisher?> _currentPublisher = new();
    private static readonly AsyncLocal<Context?> _currentContext = new();

    /// <summary>
    /// Pushes an ambient <see cref="IEventPublisher"/> into the current async flow for convenience API usage.
    /// </summary>
    public static IDisposable PushCurrent(IEventPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);

        var previous = _currentPublisher.Value;
        _currentPublisher.Value = publisher;
        return new AmbientPublisherScope(previous);
    }

    /// <summary>
    /// Resolves and pushes the current scope's <see cref="IEventPublisher"/> into the ambient async flow.
    /// </summary>
    public static IDisposable PushCurrent(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var publisher = services.GetService(typeof(IEventPublisher)) as IEventPublisher
            ?? throw new InvalidOperationException($"No service of type '{typeof(IEventPublisher).FullName}' is registered.");
        return PushCurrent(publisher);
    }

    /// <summary>
    /// Publishes an event through the ambient publisher convenience API.
    /// </summary>
    public static void PublishEvent(object payload, Type[] eventTypes, Context context)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(eventTypes);
        ArgumentNullException.ThrowIfNull(context);

        var publisher = GetCurrentPublisher();
        publisher.PublishAsync(payload, eventTypes, context, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Publishes an event through the ambient publisher with its bound context.
    /// </summary>
    public static void PublishEvent(object payload, Type[] eventTypes)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(eventTypes);

        var publisher = GetCurrentPublisher();
        publisher.PublishAsync(payload, eventTypes, GetCurrentContext(), CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static void PublishEvent(
        object payload,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType,
        Context context)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        PublishEvent(payload, runtimeType.GetAllAssignableTypes(), context);
    }

    public static void PublishEvent(
        object payload,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        PublishEvent(payload, runtimeType.GetAllAssignableTypes());
    }

    public static void PublishEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TPayload>(
        TPayload payload,
        Context context)
    {
        ArgumentNullException.ThrowIfNull(payload);
        PublishEvent(payload, typeof(TPayload), context);
    }

    public static void PublishEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TPayload>(TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        PublishEvent(payload, typeof(TPayload));
    }

    internal static IDisposable PushContext(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ReferenceEquals(_currentContext.Value, context))
        {
            return EmptyScope.Instance;
        }

        var previous = _currentContext.Value;
        _currentContext.Value = context;
        return new AmbientContextScope(previous);
    }

    internal static IDisposable PushPublisherIfNeeded(IEventPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        return ReferenceEquals(_currentPublisher.Value, publisher)
            ? EmptyScope.Instance
            : PushCurrent(publisher);
    }

    private static IEventPublisher GetCurrentPublisher()
        => _currentPublisher.Value
            ?? throw new InvalidOperationException("No ambient IEventPublisher is available for the current async flow.");

    private static Context GetCurrentContext()
        => _currentContext.Value ?? Context.Empty;

    private sealed class AmbientPublisherScope : IDisposable
    {
        private readonly IEventPublisher? _previous;
        private bool _disposed;

        public AmbientPublisherScope(IEventPublisher? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _currentPublisher.Value = _previous;
            _disposed = true;
        }
    }

    private sealed class AmbientContextScope : IDisposable
    {
        private readonly Context? _previous;
        private bool _disposed;

        public AmbientContextScope(Context? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _currentContext.Value = _previous;
            _disposed = true;
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
