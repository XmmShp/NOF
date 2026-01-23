using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NOF;

/// <summary>
/// Handler 执行器接口
/// 负责通过管道执行 Handler
/// </summary>
public interface IHandlerExecutor
{
    /// <summary>
    /// 执行 Command Handler
    /// </summary>
    ValueTask ExecuteCommandAsync<TCommand>(
        ICommandHandler<TCommand> handler,
        TCommand command,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TCommand : class, ICommand;

    /// <summary>
    /// 执行 Notification Handler
    /// </summary>                  
    ValueTask ExecuteNotificationAsync<TNotification>(
        INotificationHandler<TNotification> handler,
        TNotification notification,
        IDictionary<string, string?> headers,
        CancellationToken cancellationToken) where TNotification : class, INotification;
}

/// <summary>
/// Handler 执行器的默认实现
/// </summary>
public sealed class HandlerExecutor : IHandlerExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<Action<IHandlerPipelineBuilder, IServiceProvider>> _configureActions;

    public HandlerExecutor(
        IServiceProvider serviceProvider,
        IEnumerable<Action<IHandlerPipelineBuilder, IServiceProvider>> configureActions)
    {
        _serviceProvider = serviceProvider;
        _configureActions = configureActions.ToList();
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
            Items = headers.ToDictionary(kv => kv.Key, object? (kv) => kv.Value)
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
            Items = headers.ToDictionary(kv => kv.Key, object? (kv) => kv.Value)
        };

        var pipeline = BuildPipeline(context, ct => new ValueTask(handler.HandleAsync(notification, ct)));
        await pipeline(cancellationToken);
    }

    private HandlerDelegate BuildPipeline(HandlerContext context, HandlerDelegate handler)
    {
        var builder = new HandlerPipelineBuilder();

        // 1. Activity 追踪
        builder.Use(new ActivityTracingMiddleware());

        // 2. 自动埋点
        var logger = _serviceProvider.GetRequiredService<ILogger<AutoInstrumentationMiddleware>>();
        builder.Use(new AutoInstrumentationMiddleware(logger));

        // 3. 租户头处理
        var tenantContext = _serviceProvider.GetRequiredService<ITenantContextInternal>();
        builder.Use(new TenantHeaderMiddleware(tenantContext));

        // 4. 收件箱消息处理
        var transactionManager = _serviceProvider.GetRequiredService<ITransactionManager>();
        var inboxMessageRepository = _serviceProvider.GetRequiredService<IInboxMessageRepository>();
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var inboxLogger = _serviceProvider.GetRequiredService<ILogger<InboxHandlerMiddleware>>();
        builder.Use(new InboxHandlerMiddleware(transactionManager, inboxMessageRepository, unitOfWork, inboxLogger));

        // 5. 事务性消息上下文
        var deferredCommandSender = _serviceProvider.GetRequiredService<IDeferredCommandSender>();
        var deferredNotificationPublisher = _serviceProvider.GetRequiredService<IDeferredNotificationPublisher>();
        builder.Use(new MessageOutboxContextMiddleware(deferredCommandSender, deferredNotificationPublisher));

        // 6. 用户自定义中间件扩展点（可以多次调用 Configure 添加）
        foreach (var configure in _configureActions)
        {
            configure(builder, _serviceProvider);
        }

        return builder.Build(context, handler);
    }
}