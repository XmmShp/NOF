using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using System.Reflection;

namespace NOF.Infrastructure;

public static class InboundHandlerInvoker
{
    private static async Task ExecuteHandlerAsync(
        IServiceProvider rootServiceProvider,
        object? message,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        Action<InboundContext> contextCallback,
        Func<IServiceProvider, CancellationToken, ValueTask> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rootServiceProvider);
        ArgumentNullException.ThrowIfNull(contextCallback);
        ArgumentNullException.ThrowIfNull(terminal);

        await using var scope = rootServiceProvider.CreateAsyncScope();
        var pipeline = rootServiceProvider.GetRequiredService<IInboundPipelineExecutor>();
        var executionContext = scope.ServiceProvider.GetRequiredService<IExecutionContext>();

        if (headers is not null)
        {
            foreach (var (headerKey, value) in headers)
            {
                executionContext[headerKey] = value;
            }
        }

        var attributes = new List<Attribute>();

        if (message is not null)
        {
            attributes.AddRange(message.GetType().GetCustomAttributes(true).Cast<Attribute>());
        }

        var metadatas = new Dictionary<string, object?>();

        if (message is not null)
        {
            metadatas["MessageName"] = message.GetType().FullName;
        }

        var context = new InboundContext
        {
            Message = message,
            Services = scope.ServiceProvider,
            Attributes = attributes,
            Metadatas = metadatas
        };

        contextCallback(context);

        await pipeline.ExecuteAsync(
            context,
            ct => terminal(scope.ServiceProvider, ct),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExecuteRpcAsync(
        IServiceProvider rootServiceProvider,
        object? message,
        MethodInfo methodInfo,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        Action<InboundContext> contextCallback,
        Func<IServiceProvider, CancellationToken, ValueTask> terminal,
        CancellationToken cancellationToken)
    {
        await ExecuteHandlerAsync(
            rootServiceProvider,
            message,
            headers,
            context =>
            {
                context.Metadatas["MethodInfo"] = methodInfo;
                context.Metadatas["MethodName"] = methodInfo.DeclaringType is null
                    ? methodInfo.Name
                    : $"{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";
                context.Metadatas["ServiceName"] = methodInfo.DeclaringType?.FullName;
                context.Attributes.AddRange(methodInfo.GetCustomAttributes(true).Cast<Attribute>());
                contextCallback(context);
            },
            terminal,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExecuteCommandAsync(
        IServiceProvider rootServiceProvider,
        Type handlerType,
        object command,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await ExecuteHandlerAsync(
            rootServiceProvider,
            command,
            headers,
            context =>
            {
                context.Metadatas["HandlerType"] = handlerType;
            },
            async (sp, ct) =>
            {
                var handler = (CommandHandler)sp.GetRequiredService(handlerType);
                await handler.HandleAsync(command, ct);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExecuteNotificationToHandlerAsync(
        IServiceProvider rootServiceProvider,
        object notification,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        await ExecuteHandlerAsync(
            rootServiceProvider,
            notification,
            headers,
            context =>
            {
                context.Metadatas["HandlerType"] = handlerType;
            },
            async (sp, ct) =>
            {
                var handler = (NotificationHandler)sp.GetRequiredService(handlerType);
                await handler.HandleAsync(notification, ct);
            },
            cancellationToken).ConfigureAwait(false);
    }
}
