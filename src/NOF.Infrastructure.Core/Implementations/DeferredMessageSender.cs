using System.Diagnostics;

namespace NOF;

/// <summary>
/// 延迟命令发送器实现
/// </summary>
internal sealed class DeferredCommandSender : IDeferredCommandSender
{
    private readonly ITransactionalMessageCollector _collector;

    public DeferredCommandSender(ITransactionalMessageCollector collector)
    {
        _collector = collector;
    }

    public void Send(ICommand command, string? destinationEndpointName = null)
    {
        var currentActivity = Activity.Current;

        _collector.AddMessage(new OutboxMessage
        {
            Message = command,
            DestinationEndpointName = destinationEndpointName,
            CreatedAt = DateTimeOffset.UtcNow,
            TraceId = currentActivity?.TraceId.ToString(),
            SpanId = currentActivity?.SpanId.ToString()
        });
    }
}

/// <summary>
/// 延迟通知发布器实现
/// </summary>
internal sealed class DeferredNotificationPublisher : IDeferredNotificationPublisher
{
    private readonly ITransactionalMessageCollector _collector;

    public DeferredNotificationPublisher(ITransactionalMessageCollector collector)
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
