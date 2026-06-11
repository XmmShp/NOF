using Microsoft.EntityFrameworkCore;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class CommandSender : ICommandSender
{
    private readonly ICommandRider _rider;
    private readonly CommandOutboundPipelineExecutor _outboundPipeline;
    private readonly DbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;
    private readonly TypeResolver _typeResolver;

    public CommandSender(
        ICommandRider rider,
        CommandOutboundPipelineExecutor outboundPipeline,
        DbContext dbContext,
        IObjectSerializer objectSerializer,
        TypeResolver typeResolver)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
        _dbContext = dbContext;
        _objectSerializer = objectSerializer;
        _typeResolver = typeResolver;
    }

    public async Task DeferSend(object command, Type commandType, Context context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(context);
        var outboundContext = new CommandOutboundContext(context);

        await _outboundPipeline.ExecuteAsync(outboundContext, command, static (_, _, _) => ValueTask.CompletedTask, cancellationToken);

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
            ParentTracingInfo = Activity.Current is null ? null : new TracingInfo(Activity.Current.TraceId.ToString(), Activity.Current.SpanId.ToString())
        });
    }

    public async Task SendAsync(object command, Type commandType, Context context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(context);
        var outboundContext = new CommandOutboundContext(context);

        await _outboundPipeline.ExecuteAsync(outboundContext, command, async (_, message, ct) =>
        {
            var payload = _objectSerializer.Serialize(message, message.GetType());
            var payloadTypeName = _typeResolver.Register(message.GetType());
            var commandTypeName = _typeResolver.Register(commandType);
            await _rider.SendAsync(payload, payloadTypeName, commandTypeName, outboundContext.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
    }
}
