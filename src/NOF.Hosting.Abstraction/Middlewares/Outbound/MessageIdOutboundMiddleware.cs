using NOF.Abstraction;

namespace NOF.Hosting;

public sealed class CommandMessageIdOutboundMiddleware : ICommandOutboundMiddleware
{
    public ValueTask InvokeAsync(CommandOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        if (!context.Headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.MessageId))
        {
            context.Headers[NOFAbstractionConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString();
        }

        return next(cancellationToken);
    }
}

public sealed class NotificationMessageIdOutboundMiddleware : INotificationOutboundMiddleware
{
    public ValueTask InvokeAsync(NotificationOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        if (!context.Headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.MessageId))
        {
            context.Headers[NOFAbstractionConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString();
        }

        return next(cancellationToken);
    }
}

public sealed class RequestMessageIdOutboundMiddleware : IRequestOutboundMiddleware
{
    public ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        if (!context.Headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.MessageId))
        {
            context.Headers[NOFAbstractionConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString();
        }

        return next(cancellationToken);
    }
}
