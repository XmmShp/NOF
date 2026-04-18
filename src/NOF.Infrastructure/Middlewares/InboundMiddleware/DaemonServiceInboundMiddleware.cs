using NOF.Abstraction;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class DaemonServiceInboundMiddleware :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware,
    IBefore<InboundExceptionMiddleware>
{
    private readonly IDaemonService[] _daemonServices;

    public DaemonServiceInboundMiddleware(IEnumerable<IDaemonService> daemonServices)
    {
        _daemonServices = daemonServices as IDaemonService[] ?? daemonServices.ToArray();
    }

    public async ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        _ = _daemonServices.Length;
        await next(cancellationToken);
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        _ = _daemonServices.Length;
        await next(cancellationToken);
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        _ = _daemonServices.Length;
        await next(cancellationToken);
    }
}
