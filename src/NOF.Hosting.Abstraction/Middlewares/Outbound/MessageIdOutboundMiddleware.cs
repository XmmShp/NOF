using NOF.Abstraction;

namespace NOF.Hosting;

public sealed class MessageIdOutboundMiddleware : AllMessagesOutboundMiddleware
{
    protected override ValueTask InvokeAsyncCore(MessageOutboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken)
    {
        if (!context.Headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.MessageId))
        {
            context.Headers[NOFAbstractionConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString();
        }

        return next(cancellationToken);
    }
}
