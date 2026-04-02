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

    public CommandSender(ICommandRider rider, IOutboundPipelineExecutor outboundPipeline, IExecutionContext executionContext)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
        _executionContext = executionContext;
    }

    public async Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        var context = new OutboundContext
        {
            Message = command,
            ExecutionContext = _executionContext
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            await _rider.SendAsync(command, _executionContext, ct);
        }, cancellationToken);
    }
}
