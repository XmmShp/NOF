using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

public interface INotificationRider
{
    Task PublishAsync(INotification notification,
        IExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}
