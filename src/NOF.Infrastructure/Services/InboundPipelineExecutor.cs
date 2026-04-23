using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class CommandInboundPipelineExecutor
{
    private readonly CommandInboundPipelineTypes _middlewareTypes;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IObjectSerializer _serializer;

    public CommandInboundPipelineExecutor(
        CommandInboundPipelineTypes middlewareTypes,
        IServiceScopeFactory scopeFactory,
        IObjectSerializer serializer)
    {
        _middlewareTypes = middlewareTypes;
        _scopeFactory = scopeFactory;
        _serializer = serializer;
        _middlewareTypes.Freeze();
    }

    public async ValueTask ExecuteAsync(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().CopyHeadersFrom(headers);

        var context = CreateContext(payload, payloadTypeName, handlerType);
        HandlerDelegate pipeline = ct => ExecuteCommandHandlerAsync(scope.ServiceProvider, handlerType, context, ct);
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middlewareType = _middlewareTypes[i];
            var next = pipeline;
            pipeline = ct => ExecuteCommandMiddlewareAsync(scope.ServiceProvider, middlewareType, context, next, ct);
        }

        await pipeline(cancellationToken).ConfigureAwait(false);
    }

    private CommandInboundContext CreateContext(ReadOnlyMemory<byte> payload, string payloadTypeName, Type handlerType)
    {
        var payloadType = TypeRegistry.Resolve(payloadTypeName);
        var message = _serializer.Deserialize(payload, payloadType)
            ?? throw new InvalidOperationException($"Failed to deserialize command payload as '{payloadTypeName}'.");

        return new CommandInboundContext
        {
            Message = message,
            HandlerType = handlerType
        };
    }

    private static ValueTask ExecuteCommandHandlerAsync(
        IServiceProvider services,
        Type handlerType,
        CommandInboundContext context,
        CancellationToken cancellationToken)
    {
        var handler = (CommandHandler)services.GetRequiredService(handlerType);
        return new ValueTask(handler.HandleAsync(context.Message, cancellationToken));
    }

    private static ValueTask ExecuteCommandMiddlewareAsync(
        IServiceProvider services,
        Type middlewareType,
        CommandInboundContext context,
        HandlerDelegate next,
        CancellationToken cancellationToken)
    {
        var middleware = (ICommandInboundMiddleware)services.GetRequiredService(middlewareType);
        return middleware.InvokeAsync(context, next, cancellationToken);
    }
}

public sealed class NotificationInboundPipelineExecutor
{
    private readonly NotificationInboundPipelineTypes _middlewareTypes;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IObjectSerializer _serializer;

    public NotificationInboundPipelineExecutor(
        NotificationInboundPipelineTypes middlewareTypes,
        IServiceScopeFactory scopeFactory,
        IObjectSerializer serializer)
    {
        _middlewareTypes = middlewareTypes;
        _scopeFactory = scopeFactory;
        _serializer = serializer;
        _middlewareTypes.Freeze();
    }

    public async ValueTask ExecuteAsync(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().CopyHeadersFrom(headers);

        var context = CreateContext(payload, payloadTypeName, handlerType);
        HandlerDelegate pipeline = ct => ExecuteNotificationHandlerAsync(scope.ServiceProvider, handlerType, context, ct);
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middlewareType = _middlewareTypes[i];
            var next = pipeline;
            pipeline = ct => ExecuteNotificationMiddlewareAsync(scope.ServiceProvider, middlewareType, context, next, ct);
        }

        await pipeline(cancellationToken).ConfigureAwait(false);
    }

    private NotificationInboundContext CreateContext(ReadOnlyMemory<byte> payload, string payloadTypeName, Type handlerType)
    {
        var payloadType = TypeRegistry.Resolve(payloadTypeName);
        var message = _serializer.Deserialize(payload, payloadType)
            ?? throw new InvalidOperationException($"Failed to deserialize notification payload as '{payloadTypeName}'.");

        return new NotificationInboundContext
        {
            Message = message,
            HandlerType = handlerType
        };
    }

    private static ValueTask ExecuteNotificationHandlerAsync(
        IServiceProvider services,
        Type handlerType,
        NotificationInboundContext context,
        CancellationToken cancellationToken)
    {
        var handler = (NotificationHandler)services.GetRequiredService(handlerType);
        return new ValueTask(handler.HandleAsync(context.Message, cancellationToken));
    }

    private static ValueTask ExecuteNotificationMiddlewareAsync(
        IServiceProvider services,
        Type middlewareType,
        NotificationInboundContext context,
        HandlerDelegate next,
        CancellationToken cancellationToken)
    {
        var middleware = (INotificationInboundMiddleware)services.GetRequiredService(middlewareType);
        return middleware.InvokeAsync(context, next, cancellationToken);
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

    public async ValueTask<object?> ExecuteAsync(
        object request,
        Type handlerType,
        Type serviceType,
        string methodName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IExecutionContext>().CopyHeadersFrom(headers);

        var context = CreateContext(request, handlerType, serviceType, methodName);
        HandlerDelegate pipeline = ct => ExecuteRequestHandlerAsync(scope.ServiceProvider, handlerType, context, ct);
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middlewareType = _middlewareTypes[i];
            var next = pipeline;
            pipeline = ct => ExecuteRequestMiddlewareAsync(scope.ServiceProvider, middlewareType, context, next, ct);
        }

        await pipeline(cancellationToken).ConfigureAwait(false);
        return context.Response;
    }

    private static RequestInboundContext CreateContext(object request, Type handlerType, Type serviceType, string methodName)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        return new RequestInboundContext
        {
            Message = request,
            HandlerType = handlerType,
            ServiceType = serviceType,
            MethodName = methodName
        };
    }

    private static async ValueTask ExecuteRequestHandlerAsync(
        IServiceProvider services,
        Type handlerType,
        RequestInboundContext context,
        CancellationToken cancellationToken)
    {
        var handler = (RpcHandler)services.GetRequiredService(handlerType);
        context.Response = await handler.HandleAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }

    private static ValueTask ExecuteRequestMiddlewareAsync(
        IServiceProvider services,
        Type middlewareType,
        RequestInboundContext context,
        HandlerDelegate next,
        CancellationToken cancellationToken)
    {
        var middleware = (IRequestInboundMiddleware)services.GetRequiredService(middlewareType);
        return middleware.InvokeAsync(context, next, cancellationToken);
    }
}
