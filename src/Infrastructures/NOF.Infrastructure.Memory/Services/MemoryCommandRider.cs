using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure.Memory;

/// <summary>
/// In-memory command rider that dispatches commands directly to their typed handlers
/// resolved from DI using keyed services.
/// Uses <see cref="ICommandHandlerResolver"/> to find the correct handler by message type
/// and optional endpoint name. Creates a new DI scope per dispatch.
/// Fully AOT-compatible no reflection or <c>MakeGenericType</c> calls.
/// </summary>
public sealed class MemoryCommandRider : ICommandRider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICommandHandlerResolver _resolver;

    public MemoryCommandRider(
        IServiceScopeFactory scopeFactory,
        ICommandHandlerResolver resolver)
    {
        _scopeFactory = scopeFactory;
        _resolver = resolver;
    }

    public async Task SendAsync(ICommand command,
        IDictionary<string, string?>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var commandType = command.GetType();
        var resolved = _resolver.Resolve(commandType)
            ?? throw new InvalidOperationException(
                $"In-memory transport cannot route command '{commandType.Name}'. " +
                "No matching local handler registered. Add a message transport to enable remote dispatch.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = (ICommandHandler)scope.ServiceProvider.GetRequiredKeyedService(resolved.HandlerType, resolved.Key);
        var pipeline = scope.ServiceProvider.GetRequiredService<IInboundPipelineExecutor>();
        var context = new InboundContext
        {
            Message = command,
            HandlerType = resolved.HandlerType,
            Headers = headers is not null
                ? new Dictionary<string, string?>(headers, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };

        await pipeline.ExecuteAsync(context,
            ct => new ValueTask(handler.HandleAsync(command, ct)),
            cancellationToken).ConfigureAwait(false);
    }
}
