namespace NOF;

public interface INotificationPublisher
{
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);
}

public interface IPublisher : INotificationPublisher, IEventPublisher;

public class Publisher : IPublisher
{
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IEventPublisher _eventPublisher;
    public Publisher(INotificationPublisher notificationPublisher, IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(notificationPublisher);
        _notificationPublisher = notificationPublisher;

        ArgumentNullException.ThrowIfNull(eventPublisher);
        _eventPublisher = eventPublisher;
    }
    public Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
        => _notificationPublisher.PublishAsync(notification, cancellationToken);
    public Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
        => _eventPublisher.PublishAsync(@event, cancellationToken);
}