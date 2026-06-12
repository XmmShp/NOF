using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class CommandSender : ICommandSender
{
    private readonly ICommandRider _rider;
    private readonly IReadOnlyList<ICommandOutboundMiddleware> _middlewares;
    private readonly DbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;
    private readonly TypeResolver _typeResolver;

    public CommandSender(
        ICommandRider rider,
        IEnumerable<ICommandOutboundMiddleware> middlewares,
        DbContext dbContext,
        IObjectSerializer objectSerializer,
        TypeResolver typeResolver)
    {
        _rider = rider;
        _middlewares = new DependencyGraph<ICommandOutboundMiddleware>(middlewares).GetExecutionOrder();
        _dbContext = dbContext;
        _objectSerializer = objectSerializer;
        _typeResolver = typeResolver;
    }

    public async Task DeferSendAsync(object command, Type commandType, Context context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(context);
        var outboundContext = new CommandOutboundContext(context);

        await ExecuteAsync(outboundContext, command, static (_, _, _) => ValueTask.CompletedTask, cancellationToken);

        var payloadTypeName = _typeResolver.Register(command.GetType());
        var dispatchTypeNames = _objectSerializer.SerializeToText(new[] { _typeResolver.Register(commandType) }, typeof(string[]));

        _dbContext.Set<NOFOutboxMessage>().Add(new NOFOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageType.Command,
            PayloadType = payloadTypeName,
            DispatchTypes = dispatchTypeNames,
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
            var payloadTypeName = _typeResolver.Register(message.GetType());
            var commandTypeName = _typeResolver.Register(commandType);
            await _rider.SendAsync(payload, payloadTypeName, commandTypeName, outboundContext.Headers, ct).ConfigureAwait(false);
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
