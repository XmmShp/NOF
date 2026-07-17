using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Infrastructure;

public sealed class CommandInboundPipelineExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IObjectSerializer _serializer;
    private readonly CommandHandlerRegistry _commandHandlerRegistry;

    public CommandInboundPipelineExecutor(
        IServiceProvider serviceProvider,
        IObjectSerializer serializer,
        CommandHandlerRegistry commandHandlerRegistry)
    {
        _serviceProvider = serviceProvider;
        _serializer = serializer;
        _commandHandlerRegistry = commandHandlerRegistry;
    }

    public async ValueTask ExecuteAsync(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string handlerTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        var middlewares = new DependencyGraph<ICommandInboundMiddleware>(
            _serviceProvider.GetServices<ICommandInboundMiddleware>()).GetExecutionOrder();
        var invoker = ResolveInvoker(handlerTypeName);
        var message = invoker.Bind(payloadTypeName, payload, _serializer.Deserialize);
        var context = CreateContext(invoker.HandlerType, invoker.MessageType, headers);
        CommandHandlerDelegate terminal = (currentContext, currentMessage, ct)
            => invoker.InvokeAsync(_serviceProvider, currentMessage, currentContext, ct);
        CommandHandlerDelegate pipeline = terminal;
        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var next = pipeline;
            pipeline = (currentContext, currentMessage, ct)
                => ExecuteCommandMiddlewareAsync(_serviceProvider, middleware, currentContext, currentMessage, next, ct);
        }

        await pipeline(context, message, cancellationToken).ConfigureAwait(false);
    }

    private ICommandInboundHandlerInvoker ResolveInvoker(string handlerTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerTypeName);

        if (!_commandHandlerRegistry.TryGetInvokerType(handlerTypeName, out var invokerType))
        {
            throw new InvalidOperationException($"Command handler invoker for '{handlerTypeName}' is not registered.");
        }

        return (ICommandInboundHandlerInvoker)_serviceProvider.GetRequiredService(invokerType);
    }

    private static CommandInboundContext CreateContext(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
        Type handlerType,
        Type messageType,
        IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var methodInfo = InboundContextReflection.ResolveHandlerMethodInfo(handlerType, messageType);
        return (CommandInboundContext)new CommandInboundContext
        {
            MethodInfo = methodInfo,
            HandlerType = handlerType,
            MessageType = messageType
        }.CopyHeadersFrom(headers);
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
    private readonly IServiceProvider _serviceProvider;
    private readonly IObjectSerializer _serializer;
    private readonly NotificationHandlerRegistry _notificationHandlerRegistry;

    public NotificationInboundPipelineExecutor(
        IServiceProvider serviceProvider,
        IObjectSerializer serializer,
        NotificationHandlerRegistry notificationHandlerRegistry)
    {
        _serviceProvider = serviceProvider;
        _serializer = serializer;
        _notificationHandlerRegistry = notificationHandlerRegistry;
    }

    public async ValueTask ExecuteAsync(
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string handlerTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        var middlewares = new DependencyGraph<INotificationInboundMiddleware>(
            _serviceProvider.GetServices<INotificationInboundMiddleware>()).GetExecutionOrder();
        var invoker = ResolveInvoker(handlerTypeName);
        var message = invoker.Bind(payloadTypeName, payload, _serializer.Deserialize);
        var context = CreateContext(invoker.HandlerType, invoker.MessageType, headers);
        NotificationHandlerDelegate terminal = (currentContext, currentMessage, ct)
            => invoker.InvokeAsync(_serviceProvider, currentMessage, currentContext, ct);
        NotificationHandlerDelegate pipeline = terminal;
        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var next = pipeline;
            pipeline = (currentContext, currentMessage, ct)
                => ExecuteNotificationMiddlewareAsync(_serviceProvider, middleware, currentContext, currentMessage, next, ct);
        }

        await pipeline(context, message, cancellationToken).ConfigureAwait(false);
    }

    private INotificationInboundHandlerInvoker ResolveInvoker(string handlerTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerTypeName);

        if (!_notificationHandlerRegistry.TryGetInvokerType(handlerTypeName, out var invokerType))
        {
            throw new InvalidOperationException($"Notification handler invoker for '{handlerTypeName}' is not registered.");
        }

        return (INotificationInboundHandlerInvoker)_serviceProvider.GetRequiredService(invokerType);
    }

    private static NotificationInboundContext CreateContext(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
        Type handlerType,
        Type messageType,
        IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var methodInfo = InboundContextReflection.ResolveHandlerMethodInfo(handlerType, messageType);
        return (NotificationInboundContext)new NotificationInboundContext
        {
            MethodInfo = methodInfo,
            HandlerType = handlerType,
            MessageType = messageType
        }.CopyHeadersFrom(headers);
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
    private readonly IServiceProvider _serviceProvider;

    public RequestInboundPipelineExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async ValueTask<RequestInboundContext> ExecuteAsync(
        object request,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
        Type handlerType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        Type responseType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType,
        string methodName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        var middlewares = new DependencyGraph<IRequestInboundMiddleware>(
            _serviceProvider.GetServices<IRequestInboundMiddleware>()).GetExecutionOrder();
        var context = CreateContext(request, handlerType, responseType, serviceType, methodName, headers);
        RequestHandlerDelegate terminal = (currentContext, currentRequest, ct)
            => ExecuteRequestHandlerAsync(_serviceProvider, handlerType, currentContext, currentRequest, ct);
        RequestHandlerDelegate pipeline = terminal;
        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var next = pipeline;
            pipeline = (currentContext, currentRequest, ct)
                => ExecuteRequestMiddlewareAsync(_serviceProvider, middleware, currentContext, currentRequest, next, ct);
        }

        await pipeline(context, request, cancellationToken).ConfigureAwait(false);
        return context;
    }

    private static RequestInboundContext CreateContext(
        object request,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
        Type handlerType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        Type responseType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType,
        string methodName,
        IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(responseType);
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
            ResponseType = responseType
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

    public static MethodInfo ResolveHandlerMethodInfo(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] Type handlerType,
        Type messageType)
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
}
