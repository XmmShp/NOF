using Microsoft.EntityFrameworkCore;
using NOF.Abstraction;
using NOF.Application;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure;

public sealed class CommandSender : ICommandSender
{
    private readonly ICommandRider _rider;
    private readonly ICommandOutboundPipelineExecutor _outboundPipeline;
    private readonly IExecutionContext _executionContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly DbContext _dbContext;
    private readonly IObjectSerializer _objectSerializer;

    public CommandSender(
        ICommandRider rider,
        ICommandOutboundPipelineExecutor outboundPipeline,
        IExecutionContext executionContext,
        IServiceProvider serviceProvider,
        DbContext dbContext,
        IObjectSerializer objectSerializer)
    {
        _rider = rider;
        _outboundPipeline = outboundPipeline;
        _executionContext = executionContext;
        _serviceProvider = serviceProvider;
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

        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));
        var payloadTypeName = TypeRegistry.Register(command.GetType());
        var dispatchTypeNames = JsonSerializer.Serialize(new[] { TypeRegistry.Register(commandType) });

        _dbContext.Set<NOFOutboxMessage>().Add(new NOFOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = OutboxMessageType.Command,
            PayloadType = payloadTypeName,
            DispatchTypes = dispatchTypeNames,
            Payload = _objectSerializer.Serialize(command).ToArray(),
            Headers = JsonSerializer.Serialize(headers, headersTypeInfo),
            ParentTracingInfo = currentActivity is null ? null : new TracingInfo(currentActivity.TraceId.ToString(), currentActivity.SpanId.ToString())
        });
    }

    public async Task SendAsync(object command, Type commandType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandType);
        var context = new CommandOutboundContext
        {
            Message = command,
            Services = _serviceProvider
        };

        await _outboundPipeline.ExecuteAsync(context, async ct =>
        {
            await _rider.SendAsync(command, commandType, context.Headers, ct).ConfigureAwait(false);
        }, cancellationToken);
    }
}
