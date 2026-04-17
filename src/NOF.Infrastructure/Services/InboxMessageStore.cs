using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure;

public sealed class InboxMessageStore
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxMessageStore> _logger;

    public InboxMessageStore(IServiceProvider serviceProvider, ILogger<InboxMessageStore> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<bool> EnqueueAsync(
        Guid messageId,
        InboxMessageType messageType,
        ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string handlerTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

        var inboxMessage = new NOFInboxMessage
        {
            Id = messageId,
            MessageType = messageType,
            PayloadType = payloadTypeName,
            HandlerType = handlerTypeName,
            Payload = payload.ToArray(),
            Headers = SerializeHeaders(headers)
        };

        dbContext.Set<NOFInboxMessage>().Add(inboxMessage);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            _logger.LogDebug(
                "Inbox message {MessageId} for handler {HandlerType} already exists, skipping enqueue",
                messageId,
                handlerTypeName);
            return false;
        }
    }

    private static string SerializeHeaders(IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var dictionary = headers?.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)
            ?? new Dictionary<string, string?>(StringComparer.Ordinal);
        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));
        return JsonSerializer.Serialize(dictionary, headersTypeInfo);
    }
}
