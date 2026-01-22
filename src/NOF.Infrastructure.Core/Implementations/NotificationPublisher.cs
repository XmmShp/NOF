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
        using var activity = MessageTracing.Source.StartActivity(
            $"{MessageTracing.ActivityNames.MessageSending}: {notification.GetType().FullName}",
            ActivityKind.Producer);

        var messageId = Guid.NewGuid().ToString();
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>
        {
            [NOFConstants.MessageId] = messageId,
            [NOFConstants.TraceId] = currentActivity?.TraceId.ToString(),
            [NOFConstants.SpanId] = currentActivity?.SpanId.ToString()
        };

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(MessageTracing.Tags.MessageId, messageId);
            activity.SetTag(MessageTracing.Tags.MessageType, notification.GetType().Name);
            activity.SetTag(MessageTracing.Tags.Destination, "broadcast");
        }

        try
        {
            var result = _rider.PublishAsync(notification, headers, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
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

        var headers = new Dictionary<string, string?>
        {
            [NOFConstants.MessageId] = Guid.NewGuid().ToString()
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