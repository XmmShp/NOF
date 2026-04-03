using NOF.Contract;

namespace NOF.Infrastructure;

public interface INotificationRider
{
    Task PublishAsync(INotification notification,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default);
}
