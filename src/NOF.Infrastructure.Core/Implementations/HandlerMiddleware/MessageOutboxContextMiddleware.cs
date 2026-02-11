using NOF.Application;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Transactional message outbox context middleware.
/// Automatically creates a MessageOutboxContext scope for command handlers.
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
