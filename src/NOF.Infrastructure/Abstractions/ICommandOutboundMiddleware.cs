using NOF.Hosting;
using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask CommandOutboundHandlerDelegate(
    CommandOutboundContext context,
    object message,
    CancellationToken cancellationToken);

public interface ICommandOutboundMiddleware : ITopologizable<ICommandOutboundMiddleware>
{
    ValueTask InvokeAsync(CommandOutboundContext context, object message, CommandOutboundHandlerDelegate next, CancellationToken cancellationToken);
}
