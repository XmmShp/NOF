using NOF.Hosting;
using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask RequestHandlerDelegate(
    RequestInboundContext context,
    object request,
    CancellationToken cancellationToken);

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestInboundMiddleware : ITopologizable<IRequestInboundMiddleware>
{
    ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken);
}
