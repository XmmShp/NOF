using System.ComponentModel;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask RequestOutboundHandlerDelegate(
    RequestOutboundContext context,
    object request,
    CancellationToken cancellationToken);

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestOutboundMiddleware
{
    ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken);
}
