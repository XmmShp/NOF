using Microsoft.EntityFrameworkCore;
using NOF.Application;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class CommandSender : ICommandSender
{
    private readonly ICommandRider _rider;
    private readonly CommandOutboundPipelineExecutor _outboundPipeline;
    private readonly IExecutionContext _executionContext;
    private readonly DbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;

    public CommandSender(
        ICommandRider rider,
        CommandOutboundPipelineExecutor outboundPipeline,
        IExecutionContext executionContext,
        DbContext dbContext,
        IObjectSerializer objectSerializer)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
        _executionContext = executionContext;
        _dbContext = dbContext;
        _objectSerializer = objectSerializer;
    }

    public void DeferSend(object command, Type commandType)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandType);
        var currentActivity = Activity.Current;
        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _executionContext)
        {
            headers[kvp.Key] = kvp.Value;
        }

        var payloadTypeName = TypeRegistry.Register(command.GetType());
        var dispatchTypeNames = _objectSerializer.SerializeToText(new[] { TypeRegistry.Register(commandType) }, typeof(string[]));

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
            var payloadTypeName = TypeRegistry.Register(command.GetType());
            var commandTypeName = TypeRegistry.Register(commandType);
            await _rider.SendAsync(payload, payloadTypeName, commandTypeName, context.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
    }
}
