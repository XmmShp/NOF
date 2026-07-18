using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRider _rider;
    private readonly IReadOnlyList<INotificationOutboundMiddleware> _middlewares;
    private readonly IDbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;

    public NotificationPublisher(
        INotificationRider rider,
        IEnumerable<INotificationOutboundMiddleware> middlewares,
        IDbContext dbContext,
        IObjectSerializer objectSerializer)
    {
        _rider = rider;
        _middlewares = new DependencyGraph<INotificationOutboundMiddleware>(middlewares).GetExecutionOrder();
        _dbContext = dbContext;
        _objectSerializer = objectSerializer;
    }

    public async Task DeferPublishAsync(object notification, Type notificationType, Context context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationType);
        ArgumentNullException.ThrowIfNull(context);
        var outboundContext = new NotificationOutboundContext(context);

        await ExecuteAsync(outboundContext, notification, static (_, _, _) => ValueTask.CompletedTask, cancellationToken);

        var dispatchRoutes = _objectSerializer.SerializeToText(
            new[] { notificationType.DisplayName },
            typeof(string[]));

        _dbContext.Set<NOFOutboxMessage>().Add(new NOFOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageType.Notification,
            DispatchRoutes = dispatchRoutes,
            Payload = _objectSerializer.Serialize(notification).ToArray(),
            Headers = _objectSerializer.SerializeToText(outboundContext.Headers, typeof(Dictionary<string, string?>)),
            TraceParent = Activity.Current?.ToTraceParent()
        });
    }

    public async Task PublishAsync(object notification, Type notificationType, Context context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationType);
        ArgumentNullException.ThrowIfNull(context);
        var outboundContext = new NotificationOutboundContext(context);

        await ExecuteAsync(outboundContext, notification, async (_, message, ct) =>
        {
            var payload = _objectSerializer.Serialize(message, message.GetType());
            await _rider.PublishAsync(
                payload,
                notificationType.DisplayName,
                outboundContext.Headers,
                ct).ConfigureAwait(false);
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
