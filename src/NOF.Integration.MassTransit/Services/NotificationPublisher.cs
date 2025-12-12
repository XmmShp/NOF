using MassTransit;

namespace NOF;

public class NotificationPublisher : INotificationPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public NotificationPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task PublishAsync(INotification notification, CancellationToken cancellationToken)
    {
        await _publishEndpoint.Publish(notification as object, cancellationToken);
    }
}
