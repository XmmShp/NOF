using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Publishes notification messages to all subscribers.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>Publishes a notification with extra headers.</summary>
    Task PublishAsync(INotification notification, IDictionary<string, string?>? headers, CancellationToken cancellationToken = default);

    /// <summary>Publishes a notification.</summary>
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
        => PublishAsync(notification, null, cancellationToken);
}
