namespace NOF.Hosting;

public sealed class MessageIdOutboundMiddleware : IOutboundMiddleware
{
    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        if (!context.Headers.ContainsKey(NOFHostingConstants.Transport.Headers.MessageId))
        {
            context.Headers[NOFHostingConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString();
        }
        return next(cancellationToken);
    }
}
