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

    public CommandSender(ICommandRider rider, IOutboundPipelineExecutor outboundPipeline)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
    }

    public async Task SendAsync(ICommand command, IDictionary<string, string?>? headers, CancellationToken cancellationToken = default)
    {
        var context = new OutboundContext
        {
            Message = command,
            Headers = headers is not null
                ? new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            await _rider.SendAsync(command, context.Headers, ct);
        }, cancellationToken);
    }
}
