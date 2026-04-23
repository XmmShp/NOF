using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class CommandInboundPipelineExecutor
{
    private readonly CommandInboundPipelineTypes _middlewareTypes;
    private readonly IServiceScopeFactory _scopeFactory;

    public CommandInboundPipelineExecutor(CommandInboundPipelineTypes middlewareTypes, IServiceScopeFactory scopeFactory)
    {
        _middlewareTypes = middlewareTypes;
        _scopeFactory = scopeFactory;
        _middlewareTypes.Freeze();
    }

    public async ValueTask ExecuteAsync(
        CommandInboundContext context,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        Func<IServiceProvider, HandlerDelegate> inboundFactory,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().CopyHeadersFrom(headers);

        var pipeline = inboundFactory(scope.ServiceProvider);
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (ICommandInboundMiddleware)scope.ServiceProvider.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        await pipeline(cancellationToken).ConfigureAwait(false);
    }

}

public sealed class NotificationInboundPipelineExecutor
{
    private readonly NotificationInboundPipelineTypes _middlewareTypes;
    private readonly IServiceScopeFactory _scopeFactory;

    public NotificationInboundPipelineExecutor(NotificationInboundPipelineTypes middlewareTypes, IServiceScopeFactory scopeFactory)
    {
        _middlewareTypes = middlewareTypes;
        _scopeFactory = scopeFactory;
        _middlewareTypes.Freeze();
    }

    public async ValueTask ExecuteAsync(
        NotificationInboundContext context,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        Func<IServiceProvider, HandlerDelegate> inboundFactory,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().CopyHeadersFrom(headers);

        var pipeline = inboundFactory(scope.ServiceProvider);
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (INotificationInboundMiddleware)scope.ServiceProvider.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        await pipeline(cancellationToken).ConfigureAwait(false);
    }

}

public sealed class RequestInboundPipelineExecutor
{
    private readonly RequestInboundPipelineTypes _middlewareTypes;
    private readonly IServiceScopeFactory _scopeFactory;

    public RequestInboundPipelineExecutor(RequestInboundPipelineTypes middlewareTypes, IServiceScopeFactory scopeFactory)
    {
        _middlewareTypes = middlewareTypes;
        _scopeFactory = scopeFactory;
        _middlewareTypes.Freeze();
    }

    public async ValueTask ExecuteAsync(
        RequestInboundContext context,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        Func<IServiceProvider, HandlerDelegate> inboundFactory,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().CopyHeadersFrom(headers);

        var pipeline = inboundFactory(scope.ServiceProvider);
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (IRequestInboundMiddleware)scope.ServiceProvider.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, next, ct);
        }

        await pipeline(cancellationToken).ConfigureAwait(false);
    }

}
