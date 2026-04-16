using NOF.Abstraction;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class EventPublisherAmbientDaemonService : IScopedDaemonService, IDisposable
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

