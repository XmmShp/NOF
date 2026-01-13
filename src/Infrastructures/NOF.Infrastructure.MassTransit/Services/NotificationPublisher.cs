using MassTransit;

namespace NOF;

public class MassTransitNotificationPublisher : INotificationPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitNotificationPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task PublishAsync(INotification notification, CancellationToken cancellationToken)
    {
        await _publishEndpoint.Publish(notification as object, cancellationToken);
    }
}
