using MassTransit;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Infrastructure.Core;
using System.Diagnostics;

namespace NOF.Infrastructure.MassTransit;

internal class MassTransitRequestHandlerAdapter<THandler, TRequest> : IConsumer<TRequest>
    where THandler : IRequestHandler<TRequest>
    where TRequest : class, IRequest
{
    private readonly THandler _handler;
    private readonly IInboundPipelineExecutor _executor;

    public MassTransitRequestHandlerAdapter(THandler handler, IInboundPipelineExecutor executor)
    {
        _handler = handler;
        _executor = executor;
    }

    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var handlerContext = MassTransitAdapterHelper.BuildHandlerContext(context, _handler);

        await _executor.ExecuteAsync(handlerContext, async ct =>
        {
            handlerContext.Response = await _handler.HandleAsync(context.Message, ct);
        }, context.CancellationToken).ConfigureAwait(false);

        await context.RespondAsync(Result.From(handlerContext.Response!)).ConfigureAwait(false);
    }
}

internal class MassTransitRequestHandlerAdapter<THandler, TRequest, TResponse> : IConsumer<TRequest>
    where THandler : IRequestHandler<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    private readonly THandler _handler;
    private readonly IInboundPipelineExecutor _executor;

    public MassTransitRequestHandlerAdapter(THandler handler, IInboundPipelineExecutor executor)
    {
        _handler = handler;
        _executor = executor;
    }

    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var handlerContext = MassTransitAdapterHelper.BuildHandlerContext(context, _handler);

        await _executor.ExecuteAsync(handlerContext, async ct =>
        {
            handlerContext.Response = await _handler.HandleAsync(context.Message, ct);
        }, context.CancellationToken).ConfigureAwait(false);

        await context.RespondAsync(Result.From<TResponse>(handlerContext.Response!)).ConfigureAwait(false);
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
    private readonly IInboundPipelineExecutor _executor;

    public MassTransitCommandHandlerAdapter(THandler handler, IInboundPipelineExecutor executor)
    {
        _handler = handler;
        _executor = executor;
    }

    public async Task Consume(ConsumeContext<TCommand> context)
    {
        var handlerContext = MassTransitAdapterHelper.BuildHandlerContext(context, _handler);

        await _executor.ExecuteAsync(handlerContext,
            ct => new ValueTask(_handler.HandleAsync(context.Message, ct)),
            context.CancellationToken).ConfigureAwait(false);
    }
}

internal class MassTransitNotificationHandlerAdapter<THandler, TNotification> : IConsumer<TNotification>
    where THandler : INotificationHandler<TNotification>
    where TNotification : class, INotification
{
    private readonly THandler _handler;
    private readonly IInboundPipelineExecutor _executor;

    public MassTransitNotificationHandlerAdapter(THandler handler, IInboundPipelineExecutor executor)
    {
        _handler = handler;
        _executor = executor;
    }

    public async Task Consume(ConsumeContext<TNotification> context)
    {
        var handlerContext = MassTransitAdapterHelper.BuildHandlerContext(context, _handler);

        await _executor.ExecuteAsync(handlerContext,
            ct => new ValueTask(_handler.HandleAsync(context.Message, ct)),
            context.CancellationToken).ConfigureAwait(false);
    }
}

internal static class MassTransitAdapterHelper
{
    public static InboundContext BuildHandlerContext<TMessage>(ConsumeContext<TMessage> context, IMessageHandler handler)
        where TMessage : class
    {
        var headers = context.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString()
        );

        var activity = Activity.Current;
        if (activity is not null)
        {
            headers[NOFConstants.Headers.TraceId] = activity.TraceId.ToString();
            headers[NOFConstants.Headers.SpanId] = activity.SpanId.ToString();
        }

        return new InboundContext
        {
            Message = (IMessage)context.Message,
            Handler = handler,
            Headers = headers
        };
    }
}
