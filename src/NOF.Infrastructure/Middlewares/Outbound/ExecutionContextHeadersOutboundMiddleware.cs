using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Copies the current <see cref="IExecutionContext"/> key-values into outbound headers
/// so outbound operations can propagate tenant/tracing/auth without mutating the ambient execution context.
/// </summary>
public sealed class ExecutionContextHeadersOutboundMiddleware :
    ICommandOutboundMiddleware,
    INotificationOutboundMiddleware,
    IRequestOutboundMiddleware,
    IBefore<MessageIdOutboundMiddleware>
{
    private readonly IExecutionContext _executionContext;

    public ExecutionContextHeadersOutboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public ValueTask InvokeAsync(CommandOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        CopyHeaders(context.Headers);
        return next(cancellationToken);
    }

    public ValueTask InvokeAsync(NotificationOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        CopyHeaders(context.Headers);
        return next(cancellationToken);
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        CopyHeaders(context.Headers);
        return next(cancellationToken);
    }

    private void CopyHeaders(IDictionary<string, string?> headers)
    {
        foreach (var (k, v) in _executionContext)
        {
            if (!headers.ContainsKey(k))
            {
                headers[k] = v;
            }
        }
    }
}
