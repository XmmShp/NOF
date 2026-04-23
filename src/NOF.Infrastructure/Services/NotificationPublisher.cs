using Microsoft.EntityFrameworkCore;
using NOF.Application;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRider _rider;
    private readonly NotificationOutboundPipelineExecutor _outboundPipeline;
    private readonly IExecutionContext _executionContext;
    private readonly DbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;

    public NotificationPublisher(
        INotificationRider rider,
        NotificationOutboundPipelineExecutor outboundPipeline,
        IExecutionContext executionContext,
        DbContext dbContext,
        IObjectSerializer objectSerializer)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
        _executionContext = executionContext;
        _dbContext = dbContext;
        _objectSerializer = objectSerializer;
    }

    public void DeferPublish(object notification, Type[] notificationTypes)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationTypes);
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _executionContext)
        {
            headers[kvp.Key] = kvp.Value;
        }

        var payloadTypeName = TypeRegistry.Register(notification.GetType());
        var dispatchTypeNames = _objectSerializer.SerializeToText(
            notificationTypes.Select(TypeRegistry.Register).ToArray(),
            typeof(string[]));

        _dbContext.Set<NOFOutboxMessage>().Add(new NOFOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageType.Notification,
            PayloadType = payloadTypeName,
            DispatchTypes = dispatchTypeNames,
            Payload = _objectSerializer.Serialize(notification).ToArray(),
            Headers = _objectSerializer.SerializeToText(headers, typeof(Dictionary<string, string?>)),
            ParentTracingInfo = currentActivity is null ? null : new TracingInfo(currentActivity.TraceId.ToString(), currentActivity.SpanId.ToString())
        });
    }

    public async Task PublishAsync(object notification, Type[] notificationTypes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationTypes);
        var context = new NotificationOutboundContext
        {
            Message = notification
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            var payload = _objectSerializer.Serialize(notification, notification.GetType());
            var payloadTypeName = TypeRegistry.Register(notification.GetType());
            var notificationTypeNames = notificationTypes.Select(TypeRegistry.Register).ToArray();
            await _rider.PublishAsync(payload, payloadTypeName, notificationTypeNames, context.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
    }
}
