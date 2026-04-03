using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>
/// Command sender implementation.
/// Runs the outbound pipeline to populate headers, then dispatches via the rider.
/// </summary>
public sealed class CommandSender : ICommandSender
{
    private readonly ICommandRider _rider;
    private readonly IOutboundPipelineExecutor _outboundPipeline;
    private readonly IExecutionContext _executionContext;
    private readonly IServiceProvider _serviceProvider;

    public CommandSender(ICommandRider rider, IOutboundPipelineExecutor outboundPipeline, IExecutionContext executionContext, IServiceProvider serviceProvider)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
        _executionContext = executionContext;
        _serviceProvider = serviceProvider;
    }

    public async Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        var context = new OutboundContext
        {
            Message = command,
            ExecutionContext = (IExecutionContext)_executionContext.Clone(),
            Services = _serviceProvider
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            await _rider.SendAsync(command, context.ExecutionContext, ct);
        }, cancellationToken);
    }
}
