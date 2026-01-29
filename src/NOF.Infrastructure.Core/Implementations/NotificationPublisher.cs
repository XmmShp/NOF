using System.Diagnostics;

namespace NOF;

/// <summary>
/// 通知发布器实现
/// </summary>
public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRider _rider;
    private readonly IInvocationContext _invocationContext;

    public NotificationPublisher(INotificationRider rider, IInvocationContext invocationContext)
    {
        _rider = rider;
        _invocationContext = invocationContext;
    }

    public Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
    {
        using var activity = MessageTracing.Source.StartActivity(
            $"{MessageTracing.ActivityNames.MessageSending}: {notification.GetType().FullName}",
            ActivityKind.Producer);

        var messageId = Guid.NewGuid().ToString();
        var currentActivity = Activity.Current;
        var tenantId = _invocationContext.TenantId;

        var headers = new Dictionary<string, string?>
        {
            [NOFConstants.MessageId] = messageId,
            [NOFConstants.TraceId] = currentActivity?.TraceId.ToString(),
            [NOFConstants.SpanId] = currentActivity?.SpanId.ToString(),
            [NOFConstants.TenantId] = tenantId
        };

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(MessageTracing.Tags.MessageId, messageId);
            activity.SetTag(MessageTracing.Tags.MessageType, notification.GetType().Name);
            activity.SetTag(MessageTracing.Tags.Destination, "broadcast");

            activity.SetTag(MessageTracing.Tags.TenantId, tenantId);
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
            [NOFConstants.MessageId] = Guid.NewGuid().ToString(),
            [NOFConstants.TenantId] = tenantId
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