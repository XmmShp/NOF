using Microsoft.EntityFrameworkCore;
using NOF.Abstraction;
using NOF.Application;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRider _rider;
    private readonly NotificationOutboundPipelineExecutor _outboundPipeline;
    private readonly IContextAccessor _contextAccessor;
    private readonly DbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;
    private readonly TypeResolver _typeResolver;

    public NotificationPublisher(
        INotificationRider rider,
        NotificationOutboundPipelineExecutor outboundPipeline,
        IContextAccessor contextAccessor,
        DbContext dbContext,
        IObjectSerializer objectSerializer,
        TypeResolver typeResolver)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
        _contextAccessor = contextAccessor;
        _dbContext = dbContext;
        _objectSerializer = objectSerializer;
        _typeResolver = typeResolver;
    }

    public void DeferPublish(object notification, Type[] notificationTypes)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationTypes);
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        _contextAccessor.Context.CopyHeadersTo(headers);

        var payloadTypeName = _typeResolver.Register(notification.GetType());
        var dispatchTypeNames = _objectSerializer.SerializeToText(
            notificationTypes.Select(_typeResolver.Register).ToArray(),
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
            Message = notification,
            Context = _contextAccessor.Context
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            var payload = _objectSerializer.Serialize(notification, notification.GetType());
            var payloadTypeName = _typeResolver.Register(notification.GetType());
            var notificationTypeNames = notificationTypes.Select(_typeResolver.Register).ToArray();
            await _rider.PublishAsync(payload, payloadTypeName, notificationTypeNames, context.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
    }
}
