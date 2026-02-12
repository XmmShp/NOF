using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Handler executor interface
/// Responsible for executing handlers through the pipeline
/// </summary>
public interface IHandlerExecutor
{
    /// <summary>
    /// Execute Command Handler
    /// </summary>
    ValueTask ExecuteCommandAsync<TCommand>(
        ICommandHandler<TCommand> handler,
        TCommand command,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TCommand : class, ICommand;

    /// <summary>
    /// Execute Notification Handler
    /// </summary>                  
    ValueTask ExecuteNotificationAsync<TNotification>(
        INotificationHandler<TNotification> handler,
        TNotification notification,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TNotification : class, INotification;

    /// <summary>
    /// Execute Request Handler (no return value)
    /// </summary>
    ValueTask<Result> ExecuteRequestAsync<TRequest>(
        IRequestHandler<TRequest> handler,
        TRequest request,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TRequest : class, IRequest;

    /// <summary>
    /// Execute Request Handler (with return value)
    /// </summary>
    ValueTask<Result<TResponse>> ExecuteRequestAsync<TRequest, TResponse>(
        IRequestHandler<TRequest, TResponse> handler,
        TRequest request,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TRequest : class, IRequest<TResponse>;
}

/// <summary>
/// Default implementation of Handler executor.
/// Middleware instances are resolved from DI (scoped, like ASP.NET Core's <c>IMiddleware</c>).
/// Middleware ordering is determined at startup by the topological sort of
/// <see cref="IHandlerMiddlewareStep"/> instances, stored in <see cref="HandlerPipelineTypes"/>.
/// </summary>
public sealed class HandlerExecutor : IHandlerExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HandlerPipelineTypes _middlewareTypes;

    public HandlerExecutor(
        IServiceProvider serviceProvider,
        HandlerPipelineTypes middlewareTypes)
    {
        _serviceProvider = serviceProvider;
        _middlewareTypes = middlewareTypes;
    }

    public async ValueTask ExecuteCommandAsync<TCommand>(
        ICommandHandler<TCommand> handler,
        TCommand command,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TCommand : class, ICommand
    {
        var context = new HandlerContext
        {
            Message = command,
            Handler = handler,
            Headers = headers
        };

        var pipeline = BuildPipeline(context, ct => new ValueTask(handler.HandleAsync(command, ct)));
        await pipeline(cancellationToken);
    }

    public async ValueTask ExecuteNotificationAsync<TNotification>(
        INotificationHandler<TNotification> handler,
        TNotification notification,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TNotification : class, INotification
    {
        var context = new HandlerContext
        {
            Message = notification,
            Handler = handler,
            Headers = headers
        };

        var pipeline = BuildPipeline(context, ct => new ValueTask(handler.HandleAsync(notification, ct)));
        await pipeline(cancellationToken);
    }

    public async ValueTask<Result> ExecuteRequestAsync<TRequest>(
        IRequestHandler<TRequest> handler,
        TRequest request,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TRequest : class, IRequest
    {
        var context = new HandlerContext
        {
            Message = request,
            Handler = handler,
            Headers = headers
        };

        var pipeline = BuildPipeline(context, async ct =>
        {
            context.Response = await handler.HandleAsync(request, ct);
        });
        await pipeline(cancellationToken);

        return Result.From(context.Response!);
    }

    public async ValueTask<Result<TResponse>> ExecuteRequestAsync<TRequest, TResponse>(
        IRequestHandler<TRequest, TResponse> handler,
        TRequest request,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TRequest : class, IRequest<TResponse>
    {
        var context = new HandlerContext
        {
            Message = request,
            Handler = handler,
            Headers = headers
        };

        var pipeline = BuildPipeline(context, async ct =>
        {
            context.Response = await handler.HandleAsync(request, ct);
        });
        await pipeline(cancellationToken);

        return Result.From<TResponse>(context.Response!);
    }

    private HandlerDelegate BuildPipeline(HandlerContext context, HandlerDelegate handler)
    {
        // Resolve all middleware from DI in the order determined by the dependency graph
        var pipeline = handler;
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middleware = (IHandlerMiddleware)_serviceProvider.GetRequiredService(_middlewareTypes[i]);
            var next = pipeline;
            pipeline = ct => middleware.InvokeAsync(context, ct2 => next(ct2), ct);
        }

        return pipeline;
    }
}
