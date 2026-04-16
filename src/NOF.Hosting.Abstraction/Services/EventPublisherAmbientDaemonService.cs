using NOF.Abstraction;

namespace NOF.Hosting;

public sealed class EventPublisherAmbientDaemonService : IDaemonService, IDisposable
{
    private readonly IDisposable _scope;

    public EventPublisherAmbientDaemonService(IEventPublisher publisher)
    {
        _scope = EventPublisher.PushCurrent(publisher);
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
