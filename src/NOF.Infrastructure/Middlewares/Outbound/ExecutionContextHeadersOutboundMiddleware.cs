using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Copies the current <see cref="IExecutionContext"/> key-values into outbound headers
/// so outbound operations can propagate tenant/tracing/auth without mutating the ambient execution context.
/// </summary>
public sealed class CommandExecutionContextHeadersOutboundMiddleware : ICommandOutboundMiddleware, IBefore<CommandMessageIdOutboundMiddleware>
{
    private readonly IExecutionContext _executionContext;

    public CommandExecutionContextHeadersOutboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public ValueTask InvokeAsync(CommandOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
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

public sealed class NotificationExecutionContextHeadersOutboundMiddleware : INotificationOutboundMiddleware, IBefore<NotificationMessageIdOutboundMiddleware>
{
    private readonly IExecutionContext _executionContext;

    public NotificationExecutionContextHeadersOutboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public ValueTask InvokeAsync(NotificationOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
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

public sealed class RequestExecutionContextHeadersOutboundMiddleware : IRequestOutboundMiddleware, IBefore<RequestMessageIdOutboundMiddleware>
{
    private readonly IExecutionContext _executionContext;

    public RequestExecutionContextHeadersOutboundMiddleware(IExecutionContext executionContext)
    {
        _executionContext = executionContext;
    }

    public ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
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
