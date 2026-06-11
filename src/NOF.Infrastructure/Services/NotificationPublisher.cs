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
    private readonly DbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;
    private readonly TypeResolver _typeResolver;

    public NotificationPublisher(
        INotificationRider rider,
        NotificationOutboundPipelineExecutor outboundPipeline,
        DbContext dbContext,
        IObjectSerializer objectSerializer,
        TypeResolver typeResolver)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
        _dbContext = dbContext;
        _objectSerializer = objectSerializer;
        _typeResolver = typeResolver;
    }

    public async Task DeferPublish(object notification, Type[] notificationTypes, Context context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationTypes);
        ArgumentNullException.ThrowIfNull(context);
        var outboundContext = new NotificationOutboundContext(context);

        await _outboundPipeline.ExecuteAsync(outboundContext, notification, static (_, _, _) => ValueTask.CompletedTask, cancellationToken);

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
            Headers = _objectSerializer.SerializeToText(outboundContext.Headers, typeof(Dictionary<string, string?>)),
            ParentTracingInfo = Activity.Current is null ? null : new TracingInfo(Activity.Current.TraceId.ToString(), Activity.Current.SpanId.ToString())
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
