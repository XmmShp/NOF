using Microsoft.Extensions.DependencyInjection;
using NOF.Contract;
using NOF.Hosting;
using System.Reflection;

namespace NOF.Application;

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

        // 收集 Attributes
        var attributes = new List<Attribute>();

        // 从 Message 类型中提取 Attributes（允许 message 为 null）
        if (message is not null)
        {
            attributes.AddRange(message.GetType().GetCustomAttributes(true).Cast<Attribute>());
        }

        // 创建 Metadatas
        var metadatas = new Dictionary<string, object?>();

        // 预填充日志/链路常用名字，避免下游频繁反射
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

        // 允许外部回调修改上下文，例如添加自定义 Attributes 或 Metadatas
        contextCallback(context);

        await pipeline.ExecuteAsync(
            context,
            ct => terminal(scope.ServiceProvider, ct),
            cancellationToken).ConfigureAwait(false);
    }

    // 专门用于 RPC 调用的方法，接收 MethodInfo 参数（message 可为空）
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
                // 预填充方法/服务名称，供日志与链路使用
                context.Metadatas["MethodInfo"] = methodInfo;
                context.Metadatas["MethodName"] = methodInfo.DeclaringType is null
                    ? methodInfo.Name
                    : $"{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";
                context.Metadatas["ServiceName"] = methodInfo.DeclaringType?.FullName;
                // 从方法上提取 Attributes
                context.Attributes.AddRange(methodInfo.GetCustomAttributes(true).Cast<Attribute>());
                // 调用外部回调
                contextCallback(context);
            },
            terminal,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExecuteCommandAsync(
        IServiceProvider rootServiceProvider,
        Type handlerType,
        ICommand command,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        await ExecuteHandlerAsync(
            rootServiceProvider,
            command,
            headers,
            context =>
            {
                // 添加 HandlerType 到 Metadatas
                context.Metadatas["HandlerType"] = handlerType;
            },
            async (sp, ct) =>
            {
                var handler = (ICommandHandler)sp.GetRequiredService(handlerType);
                await handler.HandleAsync(command, ct);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExecuteNotificationToHandlerAsync(
        IServiceProvider rootServiceProvider,
        INotification notification,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        await ExecuteHandlerAsync(
            rootServiceProvider,
            notification,
            headers,
            context =>
            {
                // 添加 HandlerType 到 Metadatas
                context.Metadatas["HandlerType"] = handlerType;
            },
            async (sp, ct) =>
            {
                var handler = (INotificationHandler)sp.GetRequiredService(handlerType);
                await handler.HandleAsync(notification, ct);
            },
            cancellationToken).ConfigureAwait(false);
    }
}
