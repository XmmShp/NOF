using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// In-memory request rider that dispatches requests directly to their typed handlers
/// resolved from DI using keyed services.
/// Uses <see cref="IRequestHandlerResolver"/> to find the correct handler by message type
/// and optional endpoint name. Creates a new DI scope per dispatch to match MassTransit behavior.
/// Fully AOT-compatible — no reflection or <c>MakeGenericType</c> calls.
/// </summary>
public sealed class InMemoryRequestRider : IRequestRider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRequestHandlerResolver _resolver;

    public InMemoryRequestRider(
        IServiceScopeFactory scopeFactory,
        IRequestHandlerResolver resolver)
    {
        _scopeFactory = scopeFactory;
        _resolver = resolver;
    }

    public async Task<Result> SendAsync(IRequest request,
        IDictionary<string, string?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var resolved = _resolver.ResolveRequest(requestType, destinationEndpointName)
            ?? throw new InvalidOperationException(
                $"In-memory transport cannot route request '{requestType.Name}' " +
                $"to endpoint '{destinationEndpointName ?? "(any)"}'. " +
                "No matching local handler registered. Add a message transport (e.g. MassTransit) to enable remote dispatch.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = (IRequestHandler)scope.ServiceProvider.GetRequiredKeyedService(resolved.HandlerType, resolved.Key);
        var pipeline = scope.ServiceProvider.GetRequiredService<IInboundPipelineExecutor>();
        var context = BuildInboundContext(request, handler, headers);

        await pipeline.ExecuteAsync(context, async ct =>
        {
            context.Response = await handler.HandleAsync(request, ct);
        }, cancellationToken).ConfigureAwait(false);

        return Result.From(context.Response!);
    }

    public async Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request,
        IDictionary<string, string?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var resolved = _resolver.ResolveRequestWithResponse(requestType, destinationEndpointName)
            ?? throw new InvalidOperationException(
                $"In-memory transport cannot route request '{requestType.Name}' " +
                $"to endpoint '{destinationEndpointName ?? "(any)"}'. " +
                "No matching local handler registered. Add a message transport (e.g. MassTransit) to enable remote dispatch.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = (IRequestHandler)scope.ServiceProvider.GetRequiredKeyedService(resolved.HandlerType, resolved.Key);
        var pipeline = scope.ServiceProvider.GetRequiredService<IInboundPipelineExecutor>();
        var context = BuildInboundContext(request, handler, headers);

        await pipeline.ExecuteAsync(context, async ct =>
        {
            context.Response = await handler.HandleAsync(request, ct);
        }, cancellationToken).ConfigureAwait(false);

        return Result.From<TResponse>(context.Response!);
    }

    private static InboundContext BuildInboundContext(IRequestMarker request, IMessageHandler handler, IDictionary<string, string?>? headers)
    {
        return new InboundContext
        {
            Message = request,
            Handler = handler,
            Headers = headers is not null
                ? new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };
    }
}
