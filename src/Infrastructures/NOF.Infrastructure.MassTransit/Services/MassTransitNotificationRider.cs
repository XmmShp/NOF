using MassTransit;

namespace NOF;

/// <summary>
/// MassTransit通知传输实现
/// </summary>
public class MassTransitNotificationRider : INotificationRider
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitNotificationRider(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task PublishAsync(INotification notification,
        IDictionary<string, string?>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await _publishEndpoint.Publish(notification as object, context =>
        {
            if (headers is null)
            {
                return;
            }

            foreach (var header in headers)
            {
                context.Headers.Set(header.Key, header.Value);
            }
        }, cancellationToken);
    }
}
