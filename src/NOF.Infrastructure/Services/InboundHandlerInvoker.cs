using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

public static class InboundHandlerInvoker
{
    public static async Task ExecuteHandlerAsync(
        IServiceProvider rootServiceProvider,
        object message,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        Func<IServiceProvider, CancellationToken, ValueTask> terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rootServiceProvider);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(handlerType);
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
        var context = new InboundContext
        {
            Message = message,
            HandlerType = handlerType,
            Services = scope.ServiceProvider
        };
        await pipeline.ExecuteAsync(
            context,
            ct => terminal(scope.ServiceProvider, ct),
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
            handlerType,
            headers,
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
            handlerType,
            headers,
            async (sp, ct) =>
            {
                var handler = (INotificationHandler)sp.GetRequiredService(handlerType);
                await handler.HandleAsync(notification, ct);
            },
            cancellationToken).ConfigureAwait(false);
    }
}
