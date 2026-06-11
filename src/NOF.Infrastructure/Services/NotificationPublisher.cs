using Microsoft.EntityFrameworkCore;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
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

    public async Task PublishAsync(object notification, Type[] notificationTypes, Context context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationTypes);
        ArgumentNullException.ThrowIfNull(context);
        var outboundContext = new NotificationOutboundContext(context);

        await _outboundPipeline.ExecuteAsync(outboundContext, notification, async (_, message, ct) =>
        {
            var payload = _objectSerializer.Serialize(message, message.GetType());
            var payloadTypeName = _typeResolver.Register(message.GetType());
            var notificationTypeNames = notificationTypes.Select(_typeResolver.Register).ToArray();
            await _rider.PublishAsync(payload, payloadTypeName, notificationTypeNames, outboundContext.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
    }
}
