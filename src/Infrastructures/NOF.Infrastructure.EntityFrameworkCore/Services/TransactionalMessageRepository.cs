using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NOF.Contract.Annotations;
using System.Text.Json;

namespace NOF;

internal sealed class TransactionalMessageRepository : ITransactionalMessageRepository
{
    private readonly NOFDbContext _dbContext;
    private readonly ILogger<TransactionalMessageRepository> _logger;

    public TransactionalMessageRepository(
        NOFDbContext dbContext,
        ILogger<TransactionalMessageRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public void Add(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken)
    {
        var outboxMessages = new List<TransactionalMessage>();

        foreach (var msg in messages)
        {
            var messageType = msg.Message is ICommand
                ? OutboxMessageType.Command
                : OutboxMessageType.Notification;

            var outboxMessage = new TransactionalMessage
            {
                Id = msg.Id,
                MessageType = messageType,
                PayloadType = msg.Message.GetType().AssemblyQualifiedName!,
                Payload = Serialize(msg.Message),
                DestinationEndpointName = msg.DestinationEndpointName,
                CreatedAt = msg.CreatedAt,
                Status = OutboxMessageStatus.Pending,
                RetryCount = msg.RetryCount
            };

            outboxMessages.Add(outboxMessage);
        }

        if (outboxMessages.Count > 0)
        {
            _dbContext.TransactionalMessages.AddRange(outboxMessages);
            _logger.LogDebug("Added {Count} messages to outbox", outboxMessages.Count);
        }
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        var messages = await _dbContext.TransactionalMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .Where(m => m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var result = new List<OutboxMessage>();

        foreach (var m in messages)
        {
            try
            {
                var message = Deserialize<IMessage>(m.PayloadType, m.Payload);
                result.Add(new OutboxMessage
                {
                    Id = m.Id,
                    Message = message,
                    DestinationEndpointName = m.DestinationEndpointName,
                    CreatedAt = m.CreatedAt,
                    RetryCount = m.RetryCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize message {MessageId} of type {MessageType}",
                    m.Id, m.PayloadType);

                await MarkAsFailedAsync(m.Id, $"Deserialization failed: {ex.Message}", cancellationToken);
            }
        }

        return result;
    }

    public async Task MarkAsSentAsync(
        IEnumerable<Guid> commandIds,
        CancellationToken cancellationToken)
    {
        await _dbContext.TransactionalMessages
            .Where(m => commandIds.Contains(m.Id))
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.Status, OutboxMessageStatus.Sent)
                      .SetProperty(m => m.SentAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }

    public async Task MarkAsFailedAsync(
        Guid commandId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var message = await _dbContext.TransactionalMessages
            .FirstOrDefaultAsync(m => m.Id == commandId, cancellationToken);

        if (message != null)
        {
            message.RetryCount++;
            message.ErrorMessage = errorMessage;
            message.FailedAt = DateTimeOffset.UtcNow;

            if (message.RetryCount >= 5)
            {
                message.Status = OutboxMessageStatus.Failed;
                _logger.LogWarning("Command {CommandId} marked as permanently failed after {RetryCount} retries",
                    commandId, message.RetryCount);
            }
            else
            {
                message.Status = OutboxMessageStatus.Pending;
                _logger.LogWarning("Command {CommandId} will be retried, current retry count: {RetryCount}",
                    commandId, message.RetryCount);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> CleanupSentMessagesAsync(
        DateTimeOffset olderThan,
        CancellationToken cancellationToken)
    {
        var deletedCount = await _dbContext.TransactionalMessages
            .Where(m => m.Status == OutboxMessageStatus.Sent)
            .Where(m => m.SentAt != null && m.SentAt < olderThan)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Cleaned up {Count} sent messages older than {OlderThan}",
                deletedCount, olderThan);
        }

        return deletedCount;
    }

    private static readonly JsonSerializerOptions SerializeOptions = new() { WriteIndented = false };
    private string Serialize(object obj)
    {
        return JsonSerializer.Serialize(obj, obj.GetType(), SerializeOptions);
    }

    private T Deserialize<T>(string typeName, string payload)
    {
        var type = Type.GetType(typeName)
            ?? throw new InvalidOperationException($"Cannot resolve type: {typeName}");

        return (T)JsonSerializer.Deserialize(payload, type)!;
    }
}
