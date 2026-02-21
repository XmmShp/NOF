using MassTransit;
using NOF.Contract;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.MassTransit;

/// <summary>
/// MassTransit notification transport implementation.
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
        await _publishEndpoint.Publish(notification as object, context => context.ApplyHeaders(headers), cancellationToken);
    }
}
