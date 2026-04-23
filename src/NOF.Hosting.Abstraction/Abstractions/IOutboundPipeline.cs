using System.ComponentModel;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask HandlerDelegate(CancellationToken cancellationToken);

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestOutboundMiddleware
{
    ValueTask InvokeAsync(RequestOutboundContext context, HandlerDelegate next, CancellationToken cancellationToken);
}
