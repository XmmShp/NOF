using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

public sealed class CommandInboundPipelineExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IObjectSerializer _serializer;
    private readonly TypeResolver _typeResolver;

    public CommandInboundPipelineExecutor(
        IServiceScopeFactory scopeFactory,
        IObjectSerializer serializer,
        TypeResolver typeResolver)
    {
        _scopeFactory = scopeFactory;
        _serializer = serializer;
        _typeResolver = typeResolver;
    }

    public async ValueTask ExecuteAsync(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var middlewares = new DependencyGraph<ICommandInboundMiddleware>(
            scope.ServiceProvider.GetServices<ICommandInboundMiddleware>()).GetExecutionOrder();
        var messageType = _typeResolver.Resolve(payloadTypeName);
        var message = DeserializeMessage(payload, messageType, payloadTypeName);
        var context = CreateContext(handlerType, messageType, headers);
        CommandHandlerDelegate terminal = (currentContext, currentMessage, ct)
            => ExecuteCommandHandlerAsync(scope.ServiceProvider, handlerType, currentContext, currentMessage, ct);
        CommandHandlerDelegate pipeline = terminal;
        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var next = pipeline;
            pipeline = (currentContext, currentMessage, ct)
                => ExecuteCommandMiddlewareAsync(scope.ServiceProvider, middleware, currentContext, currentMessage, next, ct);
        }

        await pipeline(context, message, cancellationToken).ConfigureAwait(false);
    }

    private object DeserializeMessage(
        ReadOnlyMemory<byte> payload,
        Type messageType,
        string payloadTypeName)
        => _serializer.Deserialize(payload, messageType)
            ?? throw new InvalidOperationException($"Failed to deserialize command payload as '{payloadTypeName}'.");

    private static CommandInboundContext CreateContext(
        Type handlerType,
        Type messageType,
        IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var methodInfo = InboundContextReflection.ResolveHandlerMethodInfo(handlerType, messageType);
        return (CommandInboundContext)new CommandInboundContext
        {
            MethodInfo = methodInfo,
            HandlerType = handlerType,
            MessageType = messageType,
            Metadata = InboundContextReflection.BuildMetadata(handlerType, methodInfo)
        }.CopyHeadersFrom(headers);
    }

    private static async ValueTask ExecuteCommandHandlerAsync(
        IServiceProvider services,
        Type handlerType,
        CommandInboundContext context,
        object message,
        CancellationToken cancellationToken)
    {
        var handler = (CommandHandler)services.GetRequiredService(handlerType);
        await handler.HandleAsync(message, context, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask ExecuteCommandMiddlewareAsync(
        IServiceProvider services,
        ICommandInboundMiddleware middleware,
        CommandInboundContext context,
        object message,
        CommandHandlerDelegate next,
        CancellationToken cancellationToken)
    {
        await middleware.InvokeAsync(context, message, next, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class NotificationInboundPipelineExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IObjectSerializer _serializer;
    private readonly TypeResolver _typeResolver;

    public NotificationInboundPipelineExecutor(
        IServiceScopeFactory scopeFactory,
        IObjectSerializer serializer,
        TypeResolver typeResolver)
    {
        _scopeFactory = scopeFactory;
        _serializer = serializer;
        _typeResolver = typeResolver;
    }

    public async ValueTask ExecuteAsync(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var middlewares = new DependencyGraph<INotificationInboundMiddleware>(
            scope.ServiceProvider.GetServices<INotificationInboundMiddleware>()).GetExecutionOrder();
        var messageType = _typeResolver.Resolve(payloadTypeName);
        var message = DeserializeMessage(payload, messageType, payloadTypeName);
        var context = CreateContext(handlerType, messageType, headers);
        NotificationHandlerDelegate terminal = (currentContext, currentMessage, ct)
            => ExecuteNotificationHandlerAsync(scope.ServiceProvider, handlerType, currentContext, currentMessage, ct);
        NotificationHandlerDelegate pipeline = terminal;
        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var next = pipeline;
            pipeline = (currentContext, currentMessage, ct)
                => ExecuteNotificationMiddlewareAsync(scope.ServiceProvider, middleware, currentContext, currentMessage, next, ct);
        }

        await pipeline(context, message, cancellationToken).ConfigureAwait(false);
    }

    private object DeserializeMessage(
        ReadOnlyMemory<byte> payload,
        Type messageType,
        string payloadTypeName)
        => _serializer.Deserialize(payload, messageType)
            ?? throw new InvalidOperationException($"Failed to deserialize notification payload as '{payloadTypeName}'.");

    private static NotificationInboundContext CreateContext(
        Type handlerType,
        Type messageType,
        IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var methodInfo = InboundContextReflection.ResolveHandlerMethodInfo(handlerType, messageType);
        return (NotificationInboundContext)new NotificationInboundContext
        {
            MethodInfo = methodInfo,
            HandlerType = handlerType,
            MessageType = messageType,
            Metadata = InboundContextReflection.BuildMetadata(handlerType, methodInfo)
        }.CopyHeadersFrom(headers);
    }

    private static async ValueTask ExecuteNotificationHandlerAsync(
        IServiceProvider services,
        Type handlerType,
        NotificationInboundContext context,
        object message,
        CancellationToken cancellationToken)
    {
        var handler = (NotificationHandler)services.GetRequiredService(handlerType);
        await handler.HandleAsync(message, context, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask ExecuteNotificationMiddlewareAsync(
        IServiceProvider services,
        INotificationInboundMiddleware middleware,
        NotificationInboundContext context,
        object message,
        NotificationHandlerDelegate next,
        CancellationToken cancellationToken)
    {
        await middleware.InvokeAsync(context, message, next, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class RequestInboundPipelineExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RequestInboundPipelineExecutor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async ValueTask<RequestInboundContext> ExecuteAsync(
        object request,
        Type handlerType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType,
        string methodName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var middlewares = new DependencyGraph<IRequestInboundMiddleware>(
            scope.ServiceProvider.GetServices<IRequestInboundMiddleware>()).GetExecutionOrder();
        var context = CreateContext(request, handlerType, serviceType, methodName, headers);
        RequestHandlerDelegate terminal = (currentContext, currentRequest, ct)
            => ExecuteRequestHandlerAsync(scope.ServiceProvider, handlerType, currentContext, currentRequest, ct);
        RequestHandlerDelegate pipeline = terminal;
        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var next = pipeline;
            pipeline = (currentContext, currentRequest, ct)
                => ExecuteRequestMiddlewareAsync(scope.ServiceProvider, middleware, currentContext, currentRequest, next, ct);
        }

        await pipeline(context, request, cancellationToken).ConfigureAwait(false);
        return context;
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

        var serviceMethodInfo = InboundContextReflection.ResolveServiceMethodInfo(serviceType, methodName);
        var requestType = InboundContextReflection.GetHandlerRequestType(handlerType);
        if (!requestType.IsInstanceOfType(request))
        {
            throw new InvalidOperationException($"Request '{request.GetType().FullName}' is not assignable to '{requestType.FullName}'.");
        }

        var handlerMethodInfo = InboundContextReflection.ResolveHandlerMethodInfo(handlerType, requestType);
        return (RequestInboundContext)new RequestInboundContext
        {
            ServiceType = serviceType,
            ServiceMethodInfo = serviceMethodInfo,
            HandlerType = handlerType,
            HandlerMethodInfo = handlerMethodInfo,
            RequestType = requestType,
            ResponseType = InboundContextReflection.GetHandlerResponseType(handlerType),
            Metadata = InboundContextReflection.BuildMetadata(serviceType, serviceMethodInfo, handlerType, handlerMethodInfo)
        }.CopyHeadersFrom(headers);
    }

    private static async ValueTask ExecuteRequestHandlerAsync(
        IServiceProvider services,
        Type handlerType,
        RequestInboundContext context,
        object request,
        CancellationToken cancellationToken)
    {
        var handler = (RpcHandler)services.GetRequiredService(handlerType);
        var response = await handler.HandleAsync(request, context, cancellationToken).ConfigureAwait(false);
        context.SetResponse(
            response
            ?? throw new InvalidOperationException($"RPC handler '{handlerType.FullName}' returned a null response."));
    }

    private static async ValueTask ExecuteRequestMiddlewareAsync(
        IServiceProvider services,
        IRequestInboundMiddleware middleware,
        RequestInboundContext context,
        object request,
        RequestHandlerDelegate next,
        CancellationToken cancellationToken)
    {
        await middleware.InvokeAsync(context, request, next, cancellationToken).ConfigureAwait(false);
    }
}

internal static class InboundContextReflection
{
    public static MethodInfo ResolveServiceMethodInfo(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType,
        string methodName)
        => serviceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Unable to resolve service method '{serviceType.FullName}.{methodName}'.");

    public static MethodInfo ResolveHandlerMethodInfo(Type handlerType, Type messageType)
    {
        var methods = handlerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var method in methods)
        {
            if (!string.Equals(method.Name, nameof(CommandHandler.HandleAsync), StringComparison.Ordinal))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != 3)
            {
                continue;
            }

            if (parameters[0].ParameterType == messageType
                && parameters[1].ParameterType == typeof(Context)
                && parameters[2].ParameterType == typeof(CancellationToken))
            {
                return method;
            }
        }

        throw new InvalidOperationException($"Unable to resolve handler method for '{handlerType.FullName}' and message type '{messageType.FullName}'.");
    }

    public static Type GetHandlerRequestType(Type handlerType)
    {
        for (var current = handlerType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(RpcHandler<,>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        throw new InvalidOperationException($"Unable to resolve RPC request type from handler '{handlerType.FullName}'.");
    }

    public static Type GetHandlerResponseType(Type handlerType)
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

    public static IReadOnlyList<object> BuildMetadata(params MemberInfo[] members)
    {
        var metadata = new List<object>();
        foreach (var member in members)
        {
            metadata.AddRange(member.GetCustomAttributes(inherit: true));
        }

        return metadata;
    }
}
