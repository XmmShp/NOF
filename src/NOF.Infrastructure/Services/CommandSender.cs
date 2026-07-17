using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class CommandSender : ICommandSender
{
    private readonly ICommandRider _rider;
    private readonly CommandHandlerRegistry _commandHandlerRegistry;
    private readonly IReadOnlyList<ICommandOutboundMiddleware> _middlewares;
    private readonly IDbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;

    public CommandSender(
        ICommandRider rider,
        CommandHandlerRegistry commandHandlerRegistry,
        IEnumerable<ICommandOutboundMiddleware> middlewares,
        IDbContext dbContext,
        IObjectSerializer objectSerializer)
    {
        _rider = rider;
        _commandHandlerRegistry = commandHandlerRegistry;
        _middlewares = new DependencyGraph<ICommandOutboundMiddleware>(middlewares).GetExecutionOrder();
        _dbContext = dbContext;
        _objectSerializer = objectSerializer;
    }

    public async Task DeferSendAsync(object command, Type commandType, Context context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(context);
        var outboundContext = new CommandOutboundContext(context);

        await ExecuteAsync(outboundContext, command, static (_, _, _) => ValueTask.CompletedTask, cancellationToken);

        var payloadTypeName = command.GetType().DisplayName;
        var dispatchRoutes = _objectSerializer.SerializeToText(
            new[] { ResolveDispatchRoute(commandType) },
            typeof(string[]));

        _dbContext.Set<NOFOutboxMessage>().Add(new NOFOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageType.Command,
            PayloadType = payloadTypeName,
            DispatchRoutes = dispatchRoutes,
            Payload = _objectSerializer.Serialize(command).ToArray(),
            Headers = _objectSerializer.SerializeToText(outboundContext.Headers, typeof(Dictionary<string, string?>)),
            TraceParent = Activity.Current?.ToTraceParent()
        });
    }

    public async Task SendAsync(object command, Type commandType, Context context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(context);
        var outboundContext = new CommandOutboundContext(context);

        await ExecuteAsync(outboundContext, command, async (_, message, ct) =>
        {
            var payload = _objectSerializer.Serialize(message, message.GetType());
            var payloadTypeName = message.GetType().DisplayName;
            var dispatchRoute = ResolveDispatchRoute(commandType);
            await _rider.SendAsync(payload, payloadTypeName, dispatchRoute, outboundContext.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    private string ResolveDispatchRoute(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        var handlerType = _commandHandlerRegistry.GetHandlers(commandType.DisplayName).FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Command '{commandType.DisplayName}' does not have a registered handler route.");

        return handlerType.DisplayName;
    }

    private ValueTask ExecuteAsync(
        CommandOutboundContext context,
        object message,
        CommandOutboundHandlerDelegate dispatch,
        CancellationToken cancellationToken)
    {
        var pipeline = dispatch;

        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var next = pipeline;
            pipeline = (currentContext, currentMessage, ct) => middleware.InvokeAsync(currentContext, currentMessage, next, ct);
        }

        return pipeline(context, message, cancellationToken);
    }
}
