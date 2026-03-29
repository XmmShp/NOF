using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>
/// In-process request dispatcher that keeps the NOF outbound/inbound pipeline model intact.
/// </summary>
public sealed class RequestDispatcher : IRequestDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRequestHandlerResolver _resolver;
    private readonly IOutboundPipelineExecutor _outboundPipeline;

    public RequestDispatcher(
        IServiceScopeFactory scopeFactory,
        IRequestHandlerResolver resolver,
        IOutboundPipelineExecutor outboundPipeline)
    {
        _scopeFactory = scopeFactory;
        _resolver = resolver;
        _outboundPipeline = outboundPipeline;
    }

    public async Task<Result> DispatchAsync(
        object request,
        IDictionary<string, string?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var outboundContext = BuildOutboundContext(request, headers, destinationEndpointName);

        await _outboundPipeline.ExecuteAsync(outboundContext, async ct =>
        {
            var requestType = request.GetType();
            var resolved = _resolver.ResolveRequest(requestType, destinationEndpointName)
                ?? throw new InvalidOperationException(
                    $"In-memory dispatch cannot route request '{requestType.Name}' " +
                    $"to endpoint '{destinationEndpointName ?? "(any)"}'. " +
                    "No matching local handler registered.");

            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = (IRequestHandler)scope.ServiceProvider.GetRequiredKeyedService(resolved.HandlerType, resolved.Key);
            var inboundPipeline = scope.ServiceProvider.GetRequiredService<IInboundPipelineExecutor>();
            var inboundContext = BuildInboundContext(request, handler, outboundContext.Headers);

            await inboundPipeline.ExecuteAsync(inboundContext, async innerCt =>
            {
                inboundContext.Response = await handler.HandleAsync(request, innerCt).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);

            outboundContext.Response = inboundContext.Response;
        }, cancellationToken).ConfigureAwait(false);

        return Result.From(outboundContext.Response!);
    }

    public async Task<Result<TResponse>> DispatchAsync<TResponse>(
        object request,
        IDictionary<string, string?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var outboundContext = BuildOutboundContext(request, headers, destinationEndpointName);

        await _outboundPipeline.ExecuteAsync(outboundContext, async ct =>
        {
            var requestType = request.GetType();
            var resolved = _resolver.ResolveRequestWithResponse(requestType, destinationEndpointName)
                ?? throw new InvalidOperationException(
                    $"In-memory dispatch cannot route request '{requestType.Name}' " +
                    $"to endpoint '{destinationEndpointName ?? "(any)"}'. " +
                    "No matching local handler registered.");

            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = (IRequestHandler)scope.ServiceProvider.GetRequiredKeyedService(resolved.HandlerType, resolved.Key);
            var inboundPipeline = scope.ServiceProvider.GetRequiredService<IInboundPipelineExecutor>();
            var inboundContext = BuildInboundContext(request, handler, outboundContext.Headers);

            await inboundPipeline.ExecuteAsync(inboundContext, async innerCt =>
            {
                inboundContext.Response = await handler.HandleAsync(request, innerCt).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);

            outboundContext.Response = inboundContext.Response;
        }, cancellationToken).ConfigureAwait(false);

        return Result.From<TResponse>(outboundContext.Response!);
    }

    private static OutboundContext BuildOutboundContext(object request, IDictionary<string, string?>? headers, string? destinationEndpointName)
    {
        return new OutboundContext
        {
            Message = request,
            DestinationEndpointName = destinationEndpointName,
            Headers = headers is not null
                ? new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static InboundContext BuildInboundContext(object request, IMessageHandler handler, IDictionary<string, string?> headers)
    {
        return new InboundContext
        {
            Message = request,
            Handler = handler,
            Headers = headers
        };
    }
}
