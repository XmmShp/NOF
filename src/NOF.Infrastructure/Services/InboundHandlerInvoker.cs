using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using System.Reflection;

namespace NOF.Infrastructure;

public static class InboundHandlerInvoker
{
    public static async Task ExecuteRpcAsync(
        IServiceProvider rootServiceProvider,
        object? message,
        MethodInfo methodInfo,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        Func<IServiceProvider, CancellationToken, ValueTask> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rootServiceProvider);
        ArgumentNullException.ThrowIfNull(methodInfo);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(terminal);

        await using var scope = rootServiceProvider.CreateAsyncScope();
        ApplyHeaders(scope.ServiceProvider, headers);

        var attributes = GetAttributes(message, methodInfo);
        var serviceType = methodInfo.DeclaringType ?? throw new InvalidOperationException("RPC method must have a declaring type.");
        var context = new RequestInboundContext
        {
            Message = message,
            Services = scope.ServiceProvider,
            Attributes = attributes,
            HandlerType = handlerType,
            MethodInfo = methodInfo,
            ServiceType = serviceType,
            OperationName = methodInfo.Name
        };

        var pipeline = scope.ServiceProvider.GetRequiredService<IRequestInboundPipelineExecutor>();
        await pipeline.ExecuteAsync(context, ct => terminal(scope.ServiceProvider, ct), cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExecuteCommandAsync(
        IServiceProvider rootServiceProvider,
        Type handlerType,
        object command,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rootServiceProvider);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(command);

        await using var scope = rootServiceProvider.CreateAsyncScope();
        ApplyHeaders(scope.ServiceProvider, headers);

        var context = new CommandInboundContext
        {
            Message = command,
            Services = scope.ServiceProvider,
            Attributes = GetAttributes(command),
            HandlerType = handlerType
        };

        var pipeline = scope.ServiceProvider.GetRequiredService<ICommandInboundPipelineExecutor>();
        await pipeline.ExecuteAsync(context, async ct =>
        {
            var handler = (CommandHandler)scope.ServiceProvider.GetRequiredService(handlerType);
            await handler.HandleAsync(command, ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExecuteNotificationToHandlerAsync(
        IServiceProvider rootServiceProvider,
        object notification,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rootServiceProvider);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(notification);

        await using var scope = rootServiceProvider.CreateAsyncScope();
        ApplyHeaders(scope.ServiceProvider, headers);

        var context = new NotificationInboundContext
        {
            Message = notification,
            Services = scope.ServiceProvider,
            Attributes = GetAttributes(notification),
            HandlerType = handlerType
        };

        var pipeline = scope.ServiceProvider.GetRequiredService<INotificationInboundPipelineExecutor>();
        await pipeline.ExecuteAsync(context, async ct =>
        {
            var handler = (NotificationHandler)scope.ServiceProvider.GetRequiredService(handlerType);
            await handler.HandleAsync(notification, ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyHeaders(IServiceProvider services, IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var executionContext = services.GetRequiredService<IExecutionContext>();
        if (headers is null)
        {
            return;
        }

        foreach (var (headerKey, value) in headers)
        {
            executionContext[headerKey] = value;
        }
    }

    private static List<Attribute> GetAttributes(object? message, MethodInfo? methodInfo = null)
    {
        var attributes = new List<Attribute>();
        if (message is not null)
        {
            attributes.AddRange(message.GetType().GetCustomAttributes(true).Cast<Attribute>());
        }

        if (methodInfo is not null)
        {
            attributes.AddRange(methodInfo.GetCustomAttributes(true).Cast<Attribute>());
        }

        return attributes;
    }
}
