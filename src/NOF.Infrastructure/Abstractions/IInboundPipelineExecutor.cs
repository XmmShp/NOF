using NOF.Hosting;

namespace NOF.Infrastructure;

public interface ICommandInboundPipelineExecutor
{
    ValueTask ExecuteAsync(CommandInboundContext context, HandlerDelegate inbound, CancellationToken cancellationToken);
}

public interface INotificationInboundPipelineExecutor
{
    ValueTask ExecuteAsync(NotificationInboundContext context, HandlerDelegate inbound, CancellationToken cancellationToken);
}

public interface IRequestInboundPipelineExecutor
{
    ValueTask ExecuteAsync(RequestInboundContext context, HandlerDelegate inbound, CancellationToken cancellationToken);
}
