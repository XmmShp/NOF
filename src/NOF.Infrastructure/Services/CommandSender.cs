using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class CommandSender : ICommandSender
{
    private readonly ICommandRider _rider;
    private readonly IReadOnlyList<ICommandOutboundMiddleware> _middlewares;
    private readonly IDbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;
    private readonly OutboxOrderSequenceAllocator _orderSequenceAllocator;

    public CommandSender(
        ICommandRider rider,
        IEnumerable<ICommandOutboundMiddleware> middlewares,
        IDbContext dbContext,
        IObjectSerializer objectSerializer,
        OutboxOrderSequenceAllocator orderSequenceAllocator)
    {
        _rider = rider;
        _middlewares = new DependencyGraph<ICommandOutboundMiddleware>(middlewares).GetExecutionOrder();
        _dbContext = dbContext;
        _objectSerializer = objectSerializer;
        _orderSequenceAllocator = orderSequenceAllocator;
    }

    public async Task DeferSendAsync(object command, Type commandType, Context context, CancellationToken cancellationToken = default)
        => await DeferSendCoreAsync(command, commandType, null, context, false, cancellationToken);

    public async Task DeferSendOrderedAsync(
        object command,
        Type commandType,
        string orderKey,
        Context context,
        bool completesOrderKey = false,
        CancellationToken cancellationToken = default)
        => await DeferSendCoreAsync(command, commandType, orderKey, context, completesOrderKey, cancellationToken);

    private async Task DeferSendCoreAsync(
        object command,
        Type commandType,
        string? orderKey,
        Context context,
        bool completesOrderKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(context);
        var outboundContext = new CommandOutboundContext(context);

        await ExecuteAsync(outboundContext, command, static (_, _, _) => ValueTask.CompletedTask, cancellationToken);

        var dispatchRoutes = _objectSerializer.SerializeToText(
            new[] { commandType.DisplayName },
            typeof(string[]));

        var order = orderKey is null
            ? (OutboxOrder?)null
            : await _orderSequenceAllocator.AllocateAsync(orderKey, completesOrderKey, cancellationToken);

        _dbContext.Set<NOFOutboxMessage>().Add(NOFOutboxMessage.Create(
            OutboxMessageType.Command,
            dispatchRoutes,
            _objectSerializer.Serialize(command).ToArray(),
            _objectSerializer.SerializeToText(outboundContext.Headers, typeof(Dictionary<string, string?>)),
            Activity.Current?.ToTraceParent(),
            order));
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
            await _rider.SendAsync(payload, commandType.DisplayName, outboundContext.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
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
