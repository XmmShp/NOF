using MassTransit;

namespace NOF;

internal class MassTransitRequestHandlerAdapter<THandler, TRequest> : IConsumer<TRequest>
    where THandler : IRequestHandler<TRequest>
    where TRequest : class, IRequest
{
    private readonly THandler _handler;
    public MassTransitRequestHandlerAdapter(THandler consumer)
    {
        _handler = consumer;
    }

    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var response = await _handler.HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(response).ConfigureAwait(false);
    }
}

internal class MassTransitRequestHandlerAdapter<THandler, TRequest, TResponse> : IConsumer<TRequest>
    where THandler : IRequestHandler<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    private readonly THandler _handler;
    public MassTransitRequestHandlerAdapter(THandler consumer)
    {
        _handler = consumer;
    }

    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var response = await _handler.HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(response).ConfigureAwait(false);
    }
}

internal class MassTransitEventHandlerAdapter<THandler, TEvent> : IConsumer<TEvent>
    where THandler : IEventHandler<TEvent>
    where TEvent : class, IEvent
{
    private readonly THandler _handler;
    public MassTransitEventHandlerAdapter(THandler consumer)
    {
        _handler = consumer;
    }

    public Task Consume(ConsumeContext<TEvent> context)
    {
        return _handler.HandleAsync(context.Message, context.CancellationToken);
    }
}

internal class MassTransitCommandHandlerAdapter<THandler, TCommand> : IConsumer<TCommand>
    where THandler : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    private readonly THandler _handler;
    public MassTransitCommandHandlerAdapter(THandler consumer)
    {
        _handler = consumer;
    }

    public async Task Consume(ConsumeContext<TCommand> context)
    {
        await _handler.HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
    }
}

internal class MassTransitNotificationHandlerAdapter<THandler, TNotification> : IConsumer<TNotification>
    where THandler : INotificationHandler<TNotification>
    where TNotification : class, INotification
{
    private readonly THandler _handler;
    public MassTransitNotificationHandlerAdapter(THandler consumer)
    {
        _handler = consumer;
    }

    public Task Consume(ConsumeContext<TNotification> context)
    {
        return _handler.HandleAsync(context.Message, context.CancellationToken);
    }
}
