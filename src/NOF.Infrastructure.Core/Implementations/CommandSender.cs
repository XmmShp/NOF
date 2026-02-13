using NOF.Application;
using NOF.Contract;
using System.Diagnostics;

namespace NOF.Infrastructure.Core;

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

    public async Task SendAsync(ICommand command, IDictionary<string, string?>? headers, string? destinationEndpointName, CancellationToken cancellationToken = default)
    {
        var context = new OutboundContext
        {
            Message = command,
            DestinationEndpointName = destinationEndpointName,
            Headers = headers is not null
                ? new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            await _rider.SendAsync(command, context.Headers, context.DestinationEndpointName, ct);
        }, cancellationToken);
    }
}

/// <summary>
/// Deferred command sender implementation.
/// </summary>
public sealed class DeferredCommandSender : IDeferredCommandSender
{
    private readonly IOutboxMessageCollector _collector;
    private readonly IInvocationContext _invocationContext;

    public DeferredCommandSender(IOutboxMessageCollector collector, IInvocationContext invocationContext)
    {
        _collector = collector;
        _invocationContext = invocationContext;
    }

    public void Send(ICommand command, string? destinationEndpointName = null)
    {
        var currentActivity = Activity.Current;
        var tenantId = _invocationContext.TenantId;

        var headers = new Dictionary<string, string?>
        {
            [NOFInfrastructureCoreConstants.Transport.Headers.TenantId] = tenantId
        };

        _collector.AddMessage(new OutboxMessage
        {
            Message = command,
            DestinationEndpointName = destinationEndpointName,
            Headers = headers,
            TraceId = currentActivity?.TraceId,
            SpanId = currentActivity?.SpanId
        });
    }
}
