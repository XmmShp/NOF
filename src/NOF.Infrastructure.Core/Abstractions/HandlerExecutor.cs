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
