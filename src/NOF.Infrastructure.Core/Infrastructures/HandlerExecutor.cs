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
        CancellationToken cancellationToken) where TCommand : class, ICommand;

    /// <summary>
    /// 执行 Request Handler（无返回值）
    /// </summary>
    ValueTask<Result> ExecuteRequestAsync<TRequest>(
        IRequestHandler<TRequest> handler,
        TRequest request,
        CancellationToken cancellationToken) where TRequest : class, IRequest;

    /// <summary>
    /// 执行 Request Handler（有返回值）
    /// </summary>
    ValueTask<Result<TResponse>> ExecuteRequestAsync<TRequest, TResponse>(
        IRequestHandler<TRequest, TResponse> handler,
        TRequest request,
        CancellationToken cancellationToken) where TRequest : class, IRequest<TResponse>;

    /// <summary>
    /// 执行 Event Handler
    /// </summary>
    ValueTask ExecuteEventAsync<TEvent>(
        IEventHandler<TEvent> handler,
        TEvent @event,
        CancellationToken cancellationToken) where TEvent : class, IEvent;

    /// <summary>
    /// 执行 Notification Handler
    /// </summary>                  
    ValueTask ExecuteNotificationAsync<TNotification>(
        INotificationHandler<TNotification> handler,
        TNotification notification,
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
        CancellationToken cancellationToken) where TCommand : class, ICommand
    {
        var context = new HandlerContext
        {
            HandlerType = handler.GetType().Name,
            MessageType = typeof(TCommand).Name,
            Message = command,
            Handler = handler
        };

        var pipeline = BuildPipeline(context, (ct) => new ValueTask(handler.HandleAsync(command, ct)));
        await pipeline(cancellationToken);
    }

    public async ValueTask<Result> ExecuteRequestAsync<TRequest>(
        IRequestHandler<TRequest> handler,
        TRequest request,
        CancellationToken cancellationToken) where TRequest : class, IRequest
    {
        var context = new HandlerContext
        {
            HandlerType = handler.GetType().Name,
            MessageType = typeof(TRequest).Name,
            Message = request,
            Handler = handler
        };

        var result = default(Result);
        var pipeline = BuildPipeline(context, async (ct) =>
        {
            result = await handler.HandleAsync(request, ct);
        });
        await pipeline(cancellationToken);
        return result!;
    }

    public async ValueTask<Result<TResponse>> ExecuteRequestAsync<TRequest, TResponse>(
        IRequestHandler<TRequest, TResponse> handler,
        TRequest request,
        CancellationToken cancellationToken) where TRequest : class, IRequest<TResponse>
    {
        var context = new HandlerContext
        {
            HandlerType = handler.GetType().Name,
            MessageType = typeof(TRequest).Name,
            Message = request,
            Handler = handler
        };

        var result = default(Result<TResponse>);
        var pipeline = BuildPipeline(context, async (ct) =>
        {
            result = await handler.HandleAsync(request, ct);
        });
        await pipeline(cancellationToken);
        return result!;
    }

    public async ValueTask ExecuteEventAsync<TEvent>(
        IEventHandler<TEvent> handler,
        TEvent @event,
        CancellationToken cancellationToken) where TEvent : class, IEvent
    {
        var context = new HandlerContext
        {
            HandlerType = handler.GetType().Name,
            MessageType = typeof(TEvent).Name,
            Message = @event,
            Handler = handler
        };

        var pipeline = BuildPipeline(context, (ct) => new ValueTask(handler.HandleAsync(@event, ct)));
        await pipeline(cancellationToken);
    }

    public async ValueTask ExecuteNotificationAsync<TNotification>(
        INotificationHandler<TNotification> handler,
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : class, INotification
    {
        var context = new HandlerContext
        {
            HandlerType = handler.GetType().Name,
            MessageType = typeof(TNotification).Name,
            Message = notification,
            Handler = handler
        };

        var pipeline = BuildPipeline(context, (ct) => new ValueTask(handler.HandleAsync(notification, ct)));
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

        // 3. 事务性消息上下文
        var deferredCommandSender = _serviceProvider.GetRequiredService<IDeferredCommandSender>();
        var deferredNotificationPublisher = _serviceProvider.GetRequiredService<IDeferredNotificationPublisher>();
        builder.Use(new TransactionalMessageContextMiddleware(deferredCommandSender, deferredNotificationPublisher));

        // 4. 用户自定义中间件扩展点（可以多次调用 Configure 添加）
        foreach (var configure in _configureActions)
        {
            configure(builder, _serviceProvider);
        }

        return builder.Build(context, handler);
    }
}