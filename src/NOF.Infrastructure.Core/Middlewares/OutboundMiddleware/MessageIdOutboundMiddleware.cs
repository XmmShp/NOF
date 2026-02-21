using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>Assigns a unique message ID to outbound messages.</summary>
public class MessageIdOutboundMiddlewareStep : IOutboundMiddlewareStep<MessageIdOutboundMiddleware>,
    IAfter<TracingOutboundMiddlewareStep>;

/// <summary>
/// Outbound middleware that assigns a unique <see cref="NOFConstants.Headers.MessageId"/>
/// to each outbound message if not already set.
/// </summary>
public sealed class MessageIdOutboundMiddleware : IOutboundMiddleware
{
    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        context.Headers.TryAdd(NOFInfrastructureCoreConstants.Transport.Headers.MessageId, Guid.NewGuid().ToString());
        return next(cancellationToken);
    }
}
