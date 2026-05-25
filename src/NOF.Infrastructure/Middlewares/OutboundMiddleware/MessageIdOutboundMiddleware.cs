using NOF.Abstraction;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class MessageIdOutboundMiddleware : ICommandOutboundMiddleware, INotificationOutboundMiddleware
    , IRequestOutboundMiddleware
{
    public ValueTask InvokeAsync(CommandOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        EnsureMessageId(context.Headers);
        return next(cancellationToken);
    }

    public ValueTask InvokeAsync(NotificationOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        EnsureMessageId(context.Headers);
        return next(cancellationToken);
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        EnsureMessageId(context.Headers);
        return next(cancellationToken);
    }

    private static void EnsureMessageId(IDictionary<string, string?> headers)
    {
        if (!headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.MessageId))
        {
            headers[NOFAbstractionConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString();
        }
    }
}
