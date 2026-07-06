namespace NOF.Abstraction;

/// <summary>
/// Activates the ambient <see cref="IEventPublisher"/> for the current dependency injection scope.
/// </summary>
/// <remarks>
/// This is part of NOF's convenience API support. Explicit <see cref="IEventPublisher"/>
/// dependencies remain the primary runtime contract.
/// </remarks>
public sealed class EventPublisherAmbientDaemonService : IDaemonService, IDisposable
{
    private readonly IDisposable _scope;

    public EventPublisherAmbientDaemonService(IEventPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        _scope = EventPublisher.PushCurrent(publisher);
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
