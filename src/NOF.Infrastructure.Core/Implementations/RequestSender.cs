using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Request sender implementation.
/// Runs the outbound pipeline (which handles tracing, headers, etc.) then dispatches via the rider.
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

        Result? result = null;
        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            result = await _rider.SendAsync(request, context.Headers, destinationEndpointName, ct);
        }, cancellationToken);

        return result!;
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

        Result<TResponse>? result = null;
        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            result = await _rider.SendAsync(request, context.Headers, destinationEndpointName, ct);
        }, cancellationToken);

        return result!;
    }
}
