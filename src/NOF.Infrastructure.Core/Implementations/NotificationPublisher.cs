using System.Diagnostics;

namespace NOF;

/// <summary>
/// 通知发布器实现
/// </summary>
public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRider _rider;

    public NotificationPublisher(INotificationRider rider)
    {
        _rider = rider;
    }

    public Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
    {
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>
        {
            [NOFConstants.MessageId] = Guid.NewGuid().ToString(),
            [NOFConstants.TraceId] = currentActivity?.TraceId.ToString(),
            [NOFConstants.SpanId] = currentActivity?.SpanId.ToString()
        };

        return _rider.PublishAsync(notification, headers, cancellationToken);
    }
}

/// <summary>
/// 延迟通知发布器实现
/// </summary>
public sealed class DeferredNotificationPublisher : IDeferredNotificationPublisher
{
    private readonly IOutboxMessageCollector _collector;

    public DeferredNotificationPublisher(IOutboxMessageCollector collector)
    {
        _collector = collector;
    }

    public void Publish(INotification notification)
    {
        var currentActivity = Activity.Current;

        _collector.AddMessage(OutboxMessage.Create(
            id: Guid.NewGuid(),
            message: notification,
            destinationEndpointName: null,
            traceId: currentActivity?.TraceId.ToString(),
            spanId: currentActivity?.SpanId.ToString()
        ));
    }
}