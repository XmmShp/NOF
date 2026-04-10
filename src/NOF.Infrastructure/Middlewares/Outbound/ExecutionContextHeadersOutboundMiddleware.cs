using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Copies the current <see cref="IExecutionContext"/> key-values into <see cref="OutboundContext.Headers"/>
/// so outbound operations can propagate tenant/tracing/auth without mutating the ambient execution context.
/// </summary>
public sealed class ExecutionContextHeadersOutboundMiddleware : IOutboundMiddleware, IBefore<MessageIdOutboundMiddleware>
{
    private readonly IExecutionContext _executionContext;

    public ExecutionContextHeadersOutboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public ValueTask InvokeAsync(OutboundContext context, OutboundDelegate next, CancellationToken cancellationToken)
    {
        foreach (var (k, v) in _executionContext)
        {
            // OutboundContext.Headers may already contain caller-provided values (e.g., outbox messages).
            if (!context.Headers.ContainsKey(k))
            {
                context.Headers[k] = v;
            }
        }

        return next(cancellationToken);
    }
}

