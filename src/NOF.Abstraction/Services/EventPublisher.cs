using System.Diagnostics.CodeAnalysis;

namespace NOF.Abstraction;

/// <summary>
/// Provides access to the ambient <see cref="IEventPublisher"/> for the current async flow.
/// </summary>
public static class EventPublisher
{
    private static readonly AsyncLocal<IEventPublisher?> _currentPublisher = new();

    public static IDisposable PushCurrent(IEventPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);

        var previous = _currentPublisher.Value;
        _currentPublisher.Value = publisher;
        return new AmbientPublisherScope(previous);
    }

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
        PublishEvent(payload, DispatchTypeUtilities.GetSelfAndBaseTypesAndInterfaces(runtimeType));
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
