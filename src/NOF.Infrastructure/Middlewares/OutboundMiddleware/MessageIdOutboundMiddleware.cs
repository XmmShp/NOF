using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>Assigns a unique message ID to outbound messages.</summary>
public class MessageIdOutboundMiddlewareStep : IOutboundMiddlewareStep<MessageIdOutboundMiddlewareStep, MessageIdOutboundMiddleware>,
    IAfter<TracingOutboundMiddlewareStep>;

/// <summary>
/// Outbound middleware that assigns a unique <see cref="NOFConstants.Headers.MessageId"/>
/// to each outbound message if not already set.
/// </summary>
public sealed class MessageIdOutboundMiddleware : IOutboundMiddleware
{
    private readonly IExecutionContext _executionContext;

    public MessageIdOutboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        _executionContext[NOFContractConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString();
        return next(cancellationToken);
    }
}
