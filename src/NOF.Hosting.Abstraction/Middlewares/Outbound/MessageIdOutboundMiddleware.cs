namespace NOF.Hosting;

public sealed class MessageIdOutboundMiddleware : IOutboundMiddleware
{
    private readonly IExecutionContext _executionContext;

    public MessageIdOutboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        if (!_executionContext.ContainsKey(NOFHostingConstants.Transport.Headers.MessageId))
        {
            _executionContext[NOFHostingConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString();
        }
        return next(cancellationToken);
    }
}

