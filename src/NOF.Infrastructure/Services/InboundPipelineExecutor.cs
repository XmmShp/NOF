using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

public sealed class CommandInboundPipelineExecutor
{
    private readonly CommandInboundPipelineTypes _middlewareTypes;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IObjectSerializer _serializer;
    private readonly TypeResolver _typeResolver;
    public CommandInboundPipelineExecutor(
        CommandInboundPipelineTypes middlewareTypes,
        IServiceScopeFactory scopeFactory,
        IObjectSerializer serializer,
        TypeResolver typeResolver)
    {
        _middlewareTypes = middlewareTypes;
        _scopeFactory = scopeFactory;
        _serializer = serializer;
        _typeResolver = typeResolver;
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
        var context = CreateContext(payload, payloadTypeName, handlerType, headers);
        HandlerDelegate pipeline = ct => ExecuteCommandHandlerAsync(scope.ServiceProvider, handlerType, context, ct);
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middlewareType = _middlewareTypes[i];
            var next = pipeline;
            pipeline = ct => ExecuteCommandMiddlewareAsync(scope.ServiceProvider, middlewareType, context, next, ct);
        }

        await pipeline(cancellationToken).ConfigureAwait(false);
    }

    private CommandInboundContext CreateContext(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var payloadType = _typeResolver.Resolve(payloadTypeName);
        var message = _serializer.Deserialize(payload, payloadType)
            ?? throw new InvalidOperationException($"Failed to deserialize command payload as '{payloadTypeName}'.");

        return new CommandInboundContext
        {
            Message = message,
            Context = Context.Empty.CopyHeadersFrom(headers),
            HandlerType = handlerType
        };
    }

    private static ValueTask ExecuteCommandHandlerAsync(
        IServiceProvider services,
        Type handlerType,
        CommandInboundContext context,
        CancellationToken cancellationToken)
    {
        return ExecuteWithAmbientContext(services, context.Context, async () =>
        {
            var handler = (CommandHandler)services.GetRequiredService(handlerType);
            await handler.HandleAsync(context.Message, context.Context, cancellationToken).ConfigureAwait(false);
        });
    }

    private static ValueTask ExecuteCommandMiddlewareAsync(
        IServiceProvider services,
        Type middlewareType,
        CommandInboundContext context,
        HandlerDelegate next,
        CancellationToken cancellationToken)
    {
        return ExecuteWithAmbientContext(services, context.Context, async () =>
        {
            var middleware = (ICommandInboundMiddleware)services.GetRequiredService(middlewareType);
            await middleware.InvokeAsync(context, next, cancellationToken).ConfigureAwait(false);
        });
    }

    private static ValueTask ExecuteWithAmbientContext(IServiceProvider services, Context context, Func<Task> action)
    {
        var accessor = services.GetRequiredService<IContextAccessor>();
        using var scope = AmbientContext.PushCurrent(accessor, context);
        return new ValueTask(action());
    }
}

public sealed class NotificationInboundPipelineExecutor
{
    private readonly NotificationInboundPipelineTypes _middlewareTypes;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IObjectSerializer _serializer;
    private readonly TypeResolver _typeResolver;
    public NotificationInboundPipelineExecutor(
        NotificationInboundPipelineTypes middlewareTypes,
        IServiceScopeFactory scopeFactory,
        IObjectSerializer serializer,
        TypeResolver typeResolver)
    {
        _middlewareTypes = middlewareTypes;
        _scopeFactory = scopeFactory;
        _serializer = serializer;
        _typeResolver = typeResolver;
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
        var context = CreateContext(payload, payloadTypeName, handlerType, headers);
        HandlerDelegate pipeline = ct => ExecuteNotificationHandlerAsync(scope.ServiceProvider, handlerType, context, ct);
        for (var i = _middlewareTypes.Count - 1; i >= 0; i--)
        {
            var middlewareType = _middlewareTypes[i];
            var next = pipeline;
            pipeline = ct => ExecuteNotificationMiddlewareAsync(scope.ServiceProvider, middlewareType, context, next, ct);
        }

        await pipeline(cancellationToken).ConfigureAwait(false);
    }

    private NotificationInboundContext CreateContext(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var payloadType = _typeResolver.Resolve(payloadTypeName);
        var message = _serializer.Deserialize(payload, payloadType)
            ?? throw new InvalidOperationException($"Failed to deserialize notification payload as '{payloadTypeName}'.");

        return new NotificationInboundContext
        {
            Message = message,
            Context = Context.Empty.CopyHeadersFrom(headers),
            HandlerType = handlerType
        };
    }

    private static ValueTask ExecuteNotificationHandlerAsync(
        IServiceProvider services,
        Type handlerType,
        NotificationInboundContext context,
        CancellationToken cancellationToken)
    {
        return ExecuteWithAmbientContext(services, context.Context, async () =>
        {
            var handler = (NotificationHandler)services.GetRequiredService(handlerType);
            await handler.HandleAsync(context.Message, context.Context, cancellationToken).ConfigureAwait(false);
        });
    }

    private static ValueTask ExecuteNotificationMiddlewareAsync(
        IServiceProvider services,
        Type middlewareType,
        NotificationInboundContext context,
        HandlerDelegate next,
        CancellationToken cancellationToken)
    {
        return ExecuteWithAmbientContext(services, context.Context, async () =>
        {
            var middleware = (INotificationInboundMiddleware)services.GetRequiredService(middlewareType);
            await middleware.InvokeAsync(context, next, cancellationToken).ConfigureAwait(false);
        });
    }

    private static ValueTask ExecuteWithAmbientContext(IServiceProvider services, Context context, Func<Task> action)
    {
        var accessor = services.GetRequiredService<IContextAccessor>();
        using var scope = AmbientContext.PushCurrent(accessor, context);
        return new ValueTask(action());
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

    public async ValueTask<IRpcResult?> ExecuteAsync(
        object request,
        Type handlerType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType,
        string methodName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = CreateContext(request, handlerType, serviceType, methodName, headers);
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

    private static RequestInboundContext CreateContext(
        object request,
        Type handlerType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType,
        string methodName,
        IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        return new RequestInboundContext
        {
            Message = request,
            Context = Context.Empty.CopyHeadersFrom(headers),
            HandlerType = handlerType,
            ResponseType = GetHandlerResponseType(handlerType),
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
        await ExecuteWithAmbientContext(services, context.Context, async () =>
        {
            var handler = (RpcHandler)services.GetRequiredService(handlerType);
            var response = await handler.HandleAsync(context.Message, context.Context, cancellationToken).ConfigureAwait(false);
            context.Response = response
                ?? throw new InvalidOperationException($"RPC handler '{handlerType.FullName}' returned a null response.");
        }).ConfigureAwait(false);
    }

    private static ValueTask ExecuteRequestMiddlewareAsync(
        IServiceProvider services,
        Type middlewareType,
        RequestInboundContext context,
        HandlerDelegate next,
        CancellationToken cancellationToken)
    {
        return ExecuteWithAmbientContext(services, context.Context, async () =>
        {
            var middleware = (IRequestInboundMiddleware)services.GetRequiredService(middlewareType);
            await middleware.InvokeAsync(context, next, cancellationToken).ConfigureAwait(false);
        });
    }

    private static ValueTask ExecuteWithAmbientContext(IServiceProvider services, Context context, Func<Task> action)
    {
        var accessor = services.GetRequiredService<IContextAccessor>();
        using var scope = AmbientContext.PushCurrent(accessor, context);
        return new ValueTask(action());
    }

    private static Type GetHandlerResponseType(Type handlerType)
    {
        for (var current = handlerType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(RpcHandler<,>))
            {
                return current.GetGenericArguments()[1];
            }
        }

        throw new InvalidOperationException($"Unable to resolve RPC response type from handler '{handlerType.FullName}'.");
    }

}
