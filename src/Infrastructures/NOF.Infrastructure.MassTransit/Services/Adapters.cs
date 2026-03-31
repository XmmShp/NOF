using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using System.Diagnostics;

namespace NOF.Infrastructure.MassTransit;

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
            headers[NOFInfrastructureConstants.Transport.Headers.TraceId] = activity.TraceId.ToString();
            headers[NOFInfrastructureConstants.Transport.Headers.SpanId] = activity.SpanId.ToString();
        }

        return new InboundContext
        {
            Message = context.Message!,
            Handler = handler,
            Headers = headers
        };
    }
}
