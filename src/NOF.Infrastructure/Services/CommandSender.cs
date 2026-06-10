using Microsoft.EntityFrameworkCore;
using NOF.Abstraction;
using NOF.Application;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class CommandSender : ICommandSender
{
    private readonly ICommandRider _rider;
    private readonly CommandOutboundPipelineExecutor _outboundPipeline;
    private readonly NOFContext _contextAccessor;
    private readonly DbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;
    private readonly TypeResolver _typeResolver;

    public CommandSender(
        ICommandRider rider,
        CommandOutboundPipelineExecutor outboundPipeline,
        NOFContext contextAccessor,
        DbContext dbContext,
        IObjectSerializer objectSerializer,
        TypeResolver typeResolver)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
        _contextAccessor = contextAccessor;
        _dbContext = dbContext;
        _objectSerializer = objectSerializer;
        _typeResolver = typeResolver;
    }

    public void DeferSend(object command, Type commandType)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandType);
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        _contextAccessor.CopyHeadersTo(headers);

        var payloadTypeName = _typeResolver.Register(command.GetType());
        var dispatchTypeNames = _objectSerializer.SerializeToText(new[] { _typeResolver.Register(commandType) }, typeof(string[]));

        _dbContext.Set<NOFOutboxMessage>().Add(new NOFOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageType.Command,
            PayloadType = payloadTypeName,
            DispatchTypes = dispatchTypeNames,
            Payload = _objectSerializer.Serialize(command).ToArray(),
            Headers = _objectSerializer.SerializeToText(headers, typeof(Dictionary<string, string?>)),
            ParentTracingInfo = currentActivity is null ? null : new TracingInfo(currentActivity.TraceId.ToString(), currentActivity.SpanId.ToString())
        });
    }

    public async Task SendAsync(object command, Type commandType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandType);
        var context = new CommandOutboundContext
        {
            Message = command
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            var payload = _objectSerializer.Serialize(command, command.GetType());
            var payloadTypeName = _typeResolver.Register(command.GetType());
            var commandTypeName = _typeResolver.Register(commandType);
            await _rider.SendAsync(payload, payloadTypeName, commandTypeName, context.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
    }
}
