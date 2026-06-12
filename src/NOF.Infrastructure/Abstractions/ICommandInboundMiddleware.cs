using NOF.Hosting;
using System.ComponentModel;

namespace NOF.Infrastructure;

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask CommandHandlerDelegate(
    CommandInboundContext context,
    object message,
    CancellationToken cancellationToken);

public interface ICommandInboundMiddleware : ITopologizable<ICommandInboundMiddleware>
{
    ValueTask InvokeAsync(CommandInboundContext context, object message, CommandHandlerDelegate next, CancellationToken cancellationToken);
}
