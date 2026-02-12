using NOF.Application;
using NOF.Contract;
using System.Diagnostics;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Notification publisher implementation.
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
            [NOFConstants.Headers.MessageId] = messageId,
            [NOFConstants.Headers.TraceId] = currentActivity?.TraceId.ToString(),
            [NOFConstants.Headers.SpanId] = currentActivity?.SpanId.ToString(),
            [NOFConstants.Headers.TenantId] = tenantId
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
