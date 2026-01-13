using MassTransit;

namespace NOF;

internal class MassTransitRequestHandlerAdapter<THandler, TRequest> : IConsumer<TRequest>
    where THandler : IRequestHandler<TRequest>
    where TRequest : class, IRequest
{
    private readonly THandler _handler;
    private readonly IHandlerExecutor _executor;

    public MassTransitRequestHandlerAdapter(THandler handler, IHandlerExecutor executor)
    {
        _handler = handler;
        _executor = executor;
    }

    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var response = await _executor.ExecuteRequestAsync(_handler, context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(response).ConfigureAwait(false);
    }
}

internal class MassTransitRequestHandlerAdapter<THandler, TRequest, TResponse> : IConsumer<TRequest>
    where THandler : IRequestHandler<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    private readonly THandler _handler;
    private readonly IHandlerExecutor _executor;

    public MassTransitRequestHandlerAdapter(THandler handler, IHandlerExecutor executor)
    {
        _handler = handler;
        _executor = executor;
    }

    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var response = await _executor.ExecuteRequestAsync(_handler, context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(response).ConfigureAwait(false);
    }
}

internal class MassTransitEventHandlerAdapter<THandler, TEvent> : IConsumer<TEvent>
    where THandler : IEventHandler<TEvent>
    where TEvent : class, IEvent
{
    private readonly THandler _handler;
    private readonly IHandlerExecutor _executor;

    public MassTransitEventHandlerAdapter(THandler handler, IHandlerExecutor executor)
    {
        _handler = handler;
        _executor = executor;
    }

    public async Task Consume(ConsumeContext<TEvent> context)
    {
        await _executor.ExecuteEventAsync(_handler, context.Message, context.CancellationToken).ConfigureAwait(false);
    }
}

internal class MassTransitCommandHandlerAdapter<THandler, TCommand> : IConsumer<TCommand>
    where THandler : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    private readonly THandler _handler;
    private readonly IHandlerExecutor _executor;

    public MassTransitCommandHandlerAdapter(THandler handler, IHandlerExecutor executor)
    {
        _handler = handler;
        _executor = executor;
    }

    public async Task Consume(ConsumeContext<TCommand> context)
    {
        await _executor.ExecuteCommandAsync(_handler, context.Message, context.CancellationToken).ConfigureAwait(false);
    }
}

internal class MassTransitNotificationHandlerAdapter<THandler, TNotification> : IConsumer<TNotification>
    where THandler : INotificationHandler<TNotification>
    where TNotification : class, INotification
{
    private readonly THandler _handler;
    private readonly IHandlerExecutor _executor;

    public MassTransitNotificationHandlerAdapter(THandler handler, IHandlerExecutor executor)
    {
        _handler = handler;
        _executor = executor;
    }

    public async Task Consume(ConsumeContext<TNotification> context)
    {
        await _executor.ExecuteNotificationAsync(_handler, context.Message, context.CancellationToken).ConfigureAwait(false);
    }
}
