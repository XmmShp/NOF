using NOF.Application;
using NOF.Contract;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure;

/// <summary>
/// Deferred command sender implementation.
/// </summary>
public sealed class DeferredCommandSender : IDeferredCommandSender
{
    private readonly IOutboxMessageRepository _repository;
    private readonly IInvocationContext _invocationContext;
    private readonly IMessageSerializer _messageSerializer;

    public DeferredCommandSender(IOutboxMessageRepository repository, IInvocationContext invocationContext, IMessageSerializer messageSerializer)
    {
        _repository = repository;
        _invocationContext = invocationContext;
        _messageSerializer = messageSerializer;
    }

    public void Send(ICommand command)
    {
        var currentActivity = Activity.Current;
        var tenantId = _invocationContext.TenantId;

        var headers = new Dictionary<string, string?>
        {
            [NOFInfrastructureConstants.Transport.Headers.TenantId] = tenantId
        };
        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));
        var typeName = TypeRegistry.Register(command.GetType());

        _repository.Add(new NOFOutboxMessage
        {
            MessageType = OutboxMessageType.Command,
            PayloadType = typeName,
            Payload = _messageSerializer.Serialize(command),
            Headers = JsonSerializer.Serialize(headers, headersTypeInfo),
            TraceId = currentActivity?.TraceId.ToString(),
            SpanId = currentActivity?.SpanId.ToString()
        });
    }
}
