using NOF.Application;
using NOF.Contract;
using System.Diagnostics;

namespace NOF.Infrastructure.Core;

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

/// <summary>
/// Deferred notification publisher implementation.
/// </summary>
public sealed class DeferredNotificationPublisher : IDeferredNotificationPublisher
{
    private readonly IOutboxMessageCollector _collector;
    private readonly IInvocationContext _invocationContext;

    public DeferredNotificationPublisher(IOutboxMessageCollector collector, IInvocationContext invocationContext)
    {
        _collector = collector;
        _invocationContext = invocationContext;
    }

    public void Publish(INotification notification)
    {
        var currentActivity = Activity.Current;
        var tenantId = _invocationContext.TenantId;

        var headers = new Dictionary<string, string?>
        {
            [NOFConstants.Headers.MessageId] = Guid.NewGuid().ToString(),
            [NOFConstants.Headers.TenantId] = tenantId
        };

        _collector.AddMessage(new OutboxMessage
        {
            Message = notification,
            DestinationEndpointName = null,
            Headers = headers,
            TraceId = currentActivity?.TraceId,
            SpanId = currentActivity?.SpanId
        });
    }
}
