using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure;

public static class InboundHandlerInvoker
{
    public static async Task ExecuteCommandAsync(
        IServiceProvider rootServiceProvider,
        Type handlerType,
        ICommand command,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rootServiceProvider);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(command);
        await using var scope = rootServiceProvider.CreateAsyncScope();
        var handler = (ICommandHandler)scope.ServiceProvider.GetRequiredService(handlerType);
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
            Message = command,
            HandlerType = handlerType,
            Services = scope.ServiceProvider
        };
        await pipeline.ExecuteAsync(
            context,
            ct => new ValueTask(handler.HandleAsync(command, ct)),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExecuteNotificationToHandlerAsync(
        IServiceProvider rootServiceProvider,
        INotification notification,
        Type handlerType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rootServiceProvider);
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(handlerType);
        await using var scope = rootServiceProvider.CreateAsyncScope();
        var handler = (INotificationHandler)scope.ServiceProvider.GetRequiredService(handlerType);
        var pipeline = rootServiceProvider.GetRequiredService<IInboundPipelineExecutor>();
        var executionContext = scope.ServiceProvider.GetRequiredService<IExecutionContext>();
        if (headers is not null)
        {
            foreach (var (keyName, value) in headers)
            {
                executionContext[keyName] = value;
            }
        }
        var context = new InboundContext
        {
            Message = notification,
            HandlerType = handlerType,
            Services = scope.ServiceProvider
        };
        await pipeline.ExecuteAsync(
            context,
            ct => new ValueTask(handler.HandleAsync(notification, ct)),
            cancellationToken).ConfigureAwait(false);
    }
}
