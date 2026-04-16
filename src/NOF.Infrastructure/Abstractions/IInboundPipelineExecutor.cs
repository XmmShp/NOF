namespace NOF.Infrastructure;

public interface IInboundPipelineExecutor<TContext>
    where TContext : MessageInboundContext
{
    ValueTask ExecuteAsync(TContext context, InboundDelegate<TContext> inbound, CancellationToken cancellationToken);
}

public interface ICommandInboundPipelineExecutor : IInboundPipelineExecutor<CommandInboundContext>;

public interface INotificationInboundPipelineExecutor : IInboundPipelineExecutor<NotificationInboundContext>;

public interface IRequestInboundPipelineExecutor : IInboundPipelineExecutor<RequestInboundContext>;
