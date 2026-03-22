using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>
/// Notification publisher implementation.
/// Runs the outbound pipeline (which handles tracing, headers, etc.) then dispatches via the rider.
/// </summary>
public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRider _rider;
    private readonly IOutboundPipelineExecutor _outboundPipeline;

    public NotificationPublisher(INotificationRider rider, IOutboundPipelineExecutor outboundPipeline)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
    }

    public async Task PublishAsync(INotification notification, IDictionary<string, string?>? headers, CancellationToken cancellationToken = default)
    {
        var context = new OutboundContext
        {
            Message = notification,
            Headers = headers is not null
                ? new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            await _rider.PublishAsync(notification, context.Headers, ct);
        }, cancellationToken);
    }
}
