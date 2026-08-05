using NOF.Contract;
using System.ComponentModel;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestOutboundMiddleware : ITopologizable<IRequestOutboundMiddleware>
{
    ValueTask InvokeAsync(RequestOutboundContext context, object request, RequestOutboundHandlerDelegate next, CancellationToken cancellationToken);
}
