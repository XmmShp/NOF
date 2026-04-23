using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Copies the current <see cref="ITransparentInfos"/> key-values into outbound headers
/// so outbound operations can propagate tenant/tracing/auth without mutating the ambient execution context.
/// </summary>
public sealed class TransparentInfosHeadersOutboundMiddleware :
    ICommandOutboundMiddleware,
    INotificationOutboundMiddleware,
    IRequestOutboundMiddleware,
    IBefore<MessageIdOutboundMiddleware>
{
    private readonly ITransparentInfos _executionContext;

    public TransparentInfosHeadersOutboundMiddleware(ITransparentInfos executionContext)
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
        _executionContext.CopyHeadersTo(headers);
    }
}
