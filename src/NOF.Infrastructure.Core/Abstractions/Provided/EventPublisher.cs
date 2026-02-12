using NOF.Domain;

namespace NOF.Infrastructure.Core;

public interface IEventPublisher
{
    Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default);
}
