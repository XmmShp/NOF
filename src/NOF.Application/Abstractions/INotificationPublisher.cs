using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Publishes notification messages to all subscribers.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>Publishes a notification.</summary>
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);
}
