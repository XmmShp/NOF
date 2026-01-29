namespace NOF;

/// <summary>
/// 事务性消息上下文中间件
/// 为 Command Handler 自动创建 TransactionalMessageContext Scope
/// </summary>
public sealed class MessageOutboxContextMiddleware : IHandlerMiddleware
{
    private readonly IDeferredCommandSender _deferredCommandSender;
    private readonly IDeferredNotificationPublisher _deferredNotificationPublisher;

    public MessageOutboxContextMiddleware(
        IDeferredCommandSender deferredCommandSender,
        IDeferredNotificationPublisher deferredNotificationPublisher)
    {
        _deferredCommandSender = deferredCommandSender;
        _deferredNotificationPublisher = deferredNotificationPublisher;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        using var scope = MessageOutboxContext.BeginScope(_deferredCommandSender, _deferredNotificationPublisher);
        await next(cancellationToken);
    }
}
