using System.Diagnostics;

namespace NOF;

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

        _collector.AddMessage(new OutboxMessage
        {
            Message = notification,
            DestinationEndpointName = null,
            CreatedAt = DateTimeOffset.UtcNow,
            TraceId = currentActivity?.TraceId.ToString(),
            SpanId = currentActivity?.SpanId.ToString()
        });
    }
}