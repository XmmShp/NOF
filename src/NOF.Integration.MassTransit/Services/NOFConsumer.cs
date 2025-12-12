using MassTransit;

namespace NOF;

public class NOFRequestConsumer<THandler, TRequest> : IConsumer<TRequest>
    where THandler : IRequestHandler<TRequest>
    where TRequest : class, IRequest
{
    private readonly THandler _handler;
    public NOFRequestConsumer(THandler consumer)
    {
        _handler = consumer;
    }

    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var response = await _handler.HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(response).ConfigureAwait(false);
    }
}

public class NOFRequestConsumer<THandler, TRequest, TResponse> : IConsumer<TRequest>
    where THandler : IRequestHandler<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    private readonly THandler _handler;
    public NOFRequestConsumer(THandler consumer)
    {
        _handler = consumer;
    }

    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var response = await _handler.HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(response).ConfigureAwait(false);
    }
}

public class NOFEventConsumer<THandler, TEvent> : IConsumer<TEvent>
    where THandler : IEventHandler<TEvent>
    where TEvent : class, IEvent
{
    private readonly THandler _handler;
    public NOFEventConsumer(THandler consumer)
    {
        _handler = consumer;
    }

    public Task Consume(ConsumeContext<TEvent> context)
    {
        return _handler.HandleAsync(context.Message, context.CancellationToken);
    }
}

public class NOFAsyncCommandConsumer<THandler, TCommand> : IConsumer<TCommand>
    where THandler : IAsyncCommandHandler<TCommand>
    where TCommand : class, IAsyncCommand
{
    private readonly THandler _handler;
    public NOFAsyncCommandConsumer(THandler consumer)
    {
        _handler = consumer;
    }

    public Task Consume(ConsumeContext<TCommand> context)
    {
        return _handler.HandleAsync(context.Message, context.CancellationToken);
    }
}

public class NOFCommandConsumer<THandler, TCommand> : IConsumer<TCommand>
    where THandler : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    private readonly THandler _handler;
    public NOFCommandConsumer(THandler consumer)
    {
        _handler = consumer;
    }

    public async Task Consume(ConsumeContext<TCommand> context)
    {
        var response = await _handler.HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(response).ConfigureAwait(false);
    }
}

public class NOFCommandConsumer<THandler, TCommand, TResponse> : IConsumer<TCommand>
    where THandler : ICommandHandler<TCommand, TResponse>
    where TCommand : class, ICommand<TResponse>
{
    private readonly THandler _handler;
    public NOFCommandConsumer(THandler consumer)
    {
        _handler = consumer;
    }

    public async Task Consume(ConsumeContext<TCommand> context)
    {
        var response = await _handler.HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
        await context.RespondAsync(response).ConfigureAwait(false);
    }
}

public class NOFNotificationConsumer<THandler, TNotification> : IConsumer<TNotification>
    where THandler : INotificationHandler<TNotification>
    where TNotification : class, INotification
{
    private readonly THandler _handler;
    public NOFNotificationConsumer(THandler consumer)
    {
        _handler = consumer;
    }

    public Task Consume(ConsumeContext<TNotification> context)
    {
        return _handler.HandleAsync(context.Message, context.CancellationToken);
    }
}
