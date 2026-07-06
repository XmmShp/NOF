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
    public static void PublishEvent(object payload, Type[] eventTypes)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(eventTypes);

        var publisher = _currentPublisher.Value
            ?? throw new InvalidOperationException("No ambient IEventPublisher is available for the current async flow.");
        publisher.PublishAsync(payload, eventTypes, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static void PublishEvent(
        object payload,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        PublishEvent(payload, runtimeType.GetAllAssignableTypes());
    }

    public static void PublishEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TPayload>(TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        PublishEvent(payload, typeof(TPayload));
    }

    private sealed class AmbientPublisherScope : IDisposable
    {
        private readonly IEventPublisher? _previousPublisher;
        private bool _disposed;

        public AmbientPublisherScope(IEventPublisher? previousPublisher)
        {
            _previousPublisher = previousPublisher;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _currentPublisher.Value = _previousPublisher;
            _disposed = true;
        }
    }
}
