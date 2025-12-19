namespace NOF;

public interface INotificationPublisher
{
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);
}