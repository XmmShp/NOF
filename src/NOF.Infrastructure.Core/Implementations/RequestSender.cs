using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Request sender implementation.
/// Runs the outbound pipeline (which handles tracing, headers, etc.) then dispatches via the rider.
/// Outbound middleware may short-circuit by setting <see cref="OutboundContext.Response"/>.
/// </summary>
public sealed class RequestSender : IRequestSender
{
    private readonly IRequestRider _rider;
    private readonly IOutboundPipelineExecutor _outboundPipeline;

    public RequestSender(IRequestRider rider, IOutboundPipelineExecutor outboundPipeline)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
    }

    public async Task<Result> SendAsync(IRequest request, IDictionary<string, string?>? headers, string? destinationEndpointName, CancellationToken cancellationToken = default)
    {
        var context = new OutboundContext
        {
            Message = request,
            DestinationEndpointName = destinationEndpointName,
            Headers = headers is not null
                ? new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            context.Response = await _rider.SendAsync(request, context.Headers, destinationEndpointName, ct);
        }, cancellationToken);

        return Result.From(context.Response!);
    }

    public async Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, IDictionary<string, string?>? headers, string? destinationEndpointName, CancellationToken cancellationToken = default)
    {
        var context = new OutboundContext
        {
            Message = request,
            DestinationEndpointName = destinationEndpointName,
            Headers = headers is not null
                ? new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            context.Response = await _rider.SendAsync(request, context.Headers, destinationEndpointName, ct);
        }, cancellationToken);

        return Result.From<TResponse>(context.Response!);
    }
}
