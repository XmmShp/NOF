using NOF.Domain;

namespace NOF.Infrastructure.Abstraction;

public interface IEventPublisher
{
    Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default);
}
