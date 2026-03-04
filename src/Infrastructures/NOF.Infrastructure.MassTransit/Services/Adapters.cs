using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;
using NOF.Infrastructure.Core;
using System.Diagnostics;

namespace NOF.Infrastructure.MassTransit;

internal class MassTransitRequestHandlerAdapter<THandler, TRequest> : IConsumer<TRequest>
    where THandler : IRequestHandler<TRequest>
    where TRequest : class, IRequest
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IInboundPipelineExecutor _executor;
    private readonly IRequestHandlerResolver _resolver;

    public MassTransitRequestHandlerAdapter(IServiceProvider serviceProvider, IInboundPipelineExecutor executor, IRequestHandlerResolver resolver)
    {
        _serviceProvider = serviceProvider;
        _executor = executor;
        _resolver = resolver;
    }

    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var key = _resolver.ResolveRequestByHandler(typeof(THandler))!;
        var handler = _serviceProvider.GetRequiredKeyedService<THandler>(key);
        var handlerContext = MassTransitAdapterHelper.BuildHandlerContext(context, handler);

        await _executor.ExecuteAsync(handlerContext, async ct =>
        {
            handlerContext.Response = await handler.HandleAsync(context.Message, ct);
        }, context.CancellationToken).ConfigureAwait(false);

        await context.RespondAsync(Result.From(handlerContext.Response!)).ConfigureAwait(false);
    }
}

internal class MassTransitRequestHandlerAdapter<THandler, TRequest, TResponse> : IConsumer<TRequest>
    where THandler : IRequestHandler<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IInboundPipelineExecutor _executor;
    private readonly IRequestHandlerResolver _resolver;

    public MassTransitRequestHandlerAdapter(IServiceProvider serviceProvider, IInboundPipelineExecutor executor, IRequestHandlerResolver resolver)
    {
        _serviceProvider = serviceProvider;
        _executor = executor;
        _resolver = resolver;
    }

    public async Task Consume(ConsumeContext<TRequest> context)
    {
        var key = _resolver.ResolveRequestWithResponseByHandler(typeof(THandler))!;
        var handler = _serviceProvider.GetRequiredKeyedService<THandler>(key);
        var handlerContext = MassTransitAdapterHelper.BuildHandlerContext(context, handler);

        await _executor.ExecuteAsync(handlerContext, async ct =>
        {
            handlerContext.Response = await handler.HandleAsync(context.Message, ct);
        }, context.CancellationToken).ConfigureAwait(false);

        await context.RespondAsync(Result.From<TResponse>(handlerContext.Response!)).ConfigureAwait(false);
    }
}

internal class MassTransitCommandHandlerAdapter<THandler, TCommand> : IConsumer<TCommand>
    where THandler : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IInboundPipelineExecutor _executor;
    private readonly ICommandHandlerResolver _resolver;

    public MassTransitCommandHandlerAdapter(IServiceProvider serviceProvider, IInboundPipelineExecutor executor, ICommandHandlerResolver resolver)
    {
        _serviceProvider = serviceProvider;
        _executor = executor;
        _resolver = resolver;
    }

    public async Task Consume(ConsumeContext<TCommand> context)
    {
        var key = _resolver.ResolveByHandler(typeof(THandler))!;
        var handler = _serviceProvider.GetRequiredKeyedService<THandler>(key);
        var handlerContext = MassTransitAdapterHelper.BuildHandlerContext(context, handler);

        await _executor.ExecuteAsync(handlerContext,
            ct => new ValueTask(handler.HandleAsync(context.Message, ct)),
            context.CancellationToken).ConfigureAwait(false);
    }
}

internal class MassTransitNotificationHandlerAdapter<THandler, TNotification> : IConsumer<TNotification>
    where THandler : INotificationHandler<TNotification>
    where TNotification : class, INotification
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IInboundPipelineExecutor _executor;

    public MassTransitNotificationHandlerAdapter(IServiceProvider serviceProvider, IInboundPipelineExecutor executor)
    {
        _serviceProvider = serviceProvider;
        _executor = executor;
    }

    public async Task Consume(ConsumeContext<TNotification> context)
    {
        var handler = _serviceProvider.GetRequiredKeyedService<THandler>(NotificationHandlerKey.Of(typeof(TNotification)));
        var handlerContext = MassTransitAdapterHelper.BuildHandlerContext(context, handler);

        await _executor.ExecuteAsync(handlerContext,
            ct => new ValueTask(handler.HandleAsync(context.Message, ct)),
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
            headers[NOFInfrastructureCoreConstants.Transport.Headers.TraceId] = activity.TraceId.ToString();
            headers[NOFInfrastructureCoreConstants.Transport.Headers.SpanId] = activity.SpanId.ToString();
        }

        return new InboundContext
        {
            Message = (IMessage)context.Message,
            Handler = handler,
            Headers = headers
        };
    }
}
