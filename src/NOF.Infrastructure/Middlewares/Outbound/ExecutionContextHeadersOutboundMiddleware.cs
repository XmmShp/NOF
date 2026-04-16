using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Copies the current <see cref="IExecutionContext"/> key-values into outbound headers
/// so outbound operations can propagate tenant/tracing/auth without mutating the ambient execution context.
/// </summary>
public sealed class ExecutionContextHeadersOutboundMiddleware : AllMessagesOutboundMiddleware, IBefore<MessageIdOutboundMiddleware>
{
    private readonly IExecutionContext _executionContext;

    public ExecutionContextHeadersOutboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    protected override ValueTask InvokeAsyncCore(MessageOutboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken)
    {
        foreach (var (k, v) in _executionContext)
        {
            if (!context.Headers.ContainsKey(k))
            {
                context.Headers[k] = v;
            }
        }

        return next(cancellationToken);
    }
}
