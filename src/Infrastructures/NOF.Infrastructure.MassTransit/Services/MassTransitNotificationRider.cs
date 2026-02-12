using MassTransit;
using NOF.Contract;
using NOF.Infrastructure.Core;
using System.Diagnostics;

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

            var activity = Activity.Current;
            if (activity is not null)
            {
                context.Headers.Set(NOFInfrastructureCoreConstants.Transport.Headers.TraceId, activity.TraceId.ToString());
                context.Headers.Set(NOFInfrastructureCoreConstants.Transport.Headers.SpanId, activity.SpanId.ToString());
            }

        }, cancellationToken);
    }
}
