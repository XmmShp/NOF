using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRider _rider;
    private readonly IReadOnlyList<INotificationOutboundMiddleware> _middlewares;
    private readonly DbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;
    private readonly TypeResolver _typeResolver;

    public NotificationPublisher(
        INotificationRider rider,
        IEnumerable<INotificationOutboundMiddleware> middlewares,
        DbContext dbContext,
        IObjectSerializer objectSerializer,
        TypeResolver typeResolver)
    {
        _rider = rider;
        _middlewares = new DependencyGraph<INotificationOutboundMiddleware>(middlewares).GetExecutionOrder();
        _dbContext = dbContext;
        _objectSerializer = objectSerializer;
        _typeResolver = typeResolver;
    }

    public async Task DeferPublishAsync(object notification, Type[] notificationTypes, Context context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationTypes);
        ArgumentNullException.ThrowIfNull(context);
        var outboundContext = new NotificationOutboundContext(context);

        await ExecuteAsync(outboundContext, notification, static (_, _, _) => ValueTask.CompletedTask, cancellationToken);

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

        await ExecuteAsync(outboundContext, notification, async (_, message, ct) =>
        {
            var payload = _objectSerializer.Serialize(message, message.GetType());
            var payloadTypeName = _typeResolver.Register(message.GetType());
            var notificationTypeNames = notificationTypes.Select(_typeResolver.Register).ToArray();
            await _rider.PublishAsync(payload, payloadTypeName, notificationTypeNames, outboundContext.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    private ValueTask ExecuteAsync(
        NotificationOutboundContext context,
        object message,
        NotificationOutboundHandlerDelegate dispatch,
        CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var next = pipeline;
            pipeline = (currentContext, currentMessage, ct) => middleware.InvokeAsync(currentContext, currentMessage, next, ct);
        }

        return pipeline(context, message, cancellationToken);
    }
}
