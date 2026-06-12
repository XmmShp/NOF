using NOF.Abstraction;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class MessageIdOutboundMiddleware :
    ICommandOutboundMiddleware,
    INotificationOutboundMiddleware
{
    public TopologyComparison Compare(ICommandOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

    public TopologyComparison Compare(INotificationOutboundMiddleware other) => TopologyComparison.DoesNotMatter;

    public ValueTask InvokeAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        EnsureMessageId(context.Headers);
        return next(context, message, cancellationToken);
    }

    public ValueTask InvokeAsync(NotificationOutboundContext context, object message, NotificationOutboundHandlerDelegate next, CancellationToken cancellationToken)
    {
        EnsureMessageId(context.Headers);
        return next(context, message, cancellationToken);
    }

    private static void EnsureMessageId(IDictionary<string, string?> headers)
    {
        if (!headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.MessageId))
        {
            headers[NOFAbstractionConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString();
        }
    }
}
