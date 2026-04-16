using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class MemoryCommandRider : ICommandRider
{
    private readonly IServiceProvider _rootServiceProvider;

    public MemoryCommandRider(
        IServiceProvider rootServiceProvider)
    {
        _rootServiceProvider = rootServiceProvider;
    }

    public async Task SendAsync(object command,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var handlerInfos = _rootServiceProvider.GetService<HandlerInfos>();
        var commandType = command.GetType();
        var handlerType = handlerInfos?.GetCommandHandlers(commandType).FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"In-memory transport cannot route command '{commandType.Name}'. No matching local handler registered.");
        await InboundHandlerInvoker.ExecuteCommandAsync(
            _rootServiceProvider,
            handlerType,
            command,
            headers,
            cancellationToken).ConfigureAwait(false);
    }
}
