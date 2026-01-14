using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Contract.Annotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF;

internal sealed class TransactionalMessageRepository : ITransactionalMessageRepository
{
    private readonly NOFDbContext _dbContext;
    private readonly OutboxOptions _options;
    private readonly ILogger<TransactionalMessageRepository> _logger;

    public TransactionalMessageRepository(
        NOFDbContext dbContext,
        IOptions<OutboxOptions> options,
        ILogger<TransactionalMessageRepository> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public void Add(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default)
    {
        var outboxMessages = new List<TransactionalMessage>();

        foreach (var msg in messages)
        {
            var messageType = msg.Message is ICommand
                ? OutboxMessageType.Command
                : OutboxMessageType.Notification;

            outboxMessages.Add(new TransactionalMessage
            {
                Id = msg.Id,
                MessageType = messageType,
                PayloadType = msg.Message.GetType().AssemblyQualifiedName!,
                Payload = Serialize(msg.Message),
                DestinationEndpointName = msg.DestinationEndpointName,
                CreatedAt = msg.CreatedAt,
                Status = OutboxMessageStatus.Pending,
                RetryCount = msg.RetryCount,
                NextTryAt = null
            });
        }

        if (outboxMessages.Count > 0)
        {
            _dbContext.TransactionalMessages.AddRange(outboxMessages);
            _logger.LogDebug("Added {Count} messages to outbox", outboxMessages.Count);
        }
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // 只获取：状态为 Pending、未达最大重试、且到了下次重试时间的消息
        var dbMessages = await _dbContext.TransactionalMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .Where(m => m.RetryCount < _options.MaxRetryCount)
            .Where(m => !m.NextTryAt.HasValue || m.NextTryAt <= DateTimeOffset.UtcNow)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var result = new List<OutboxMessage>(dbMessages.Count);

        foreach (var m in dbMessages)
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
                _logger.LogError(ex, "Deserialization failed for message {MessageId}, type: {TypeName}",
                    m.Id, m.PayloadType);

                // 即使反序列化失败，也应标记为永久失败（避免无限重试）
                await MarkAsPermanentlyFailedAsync(m.Id, $"Deserialization error: {ex.Message}", cancellationToken);
            }
        }

        return result;
    }

    public async Task MarkAsSentAsync(
        IEnumerable<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        // 避免重复标记（如已被其他实例处理）
        await _dbContext.TransactionalMessages
            .Where(m => messageIds.Contains(m.Id) && m.Status == OutboxMessageStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxMessageStatus.Sent)
                .SetProperty(m => m.SentAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }

    /// <summary>
    /// 记录发送失败，并根据重试次数决定是否继续重试或永久失败。
    /// </summary>
    public async Task RecordDeliveryFailureAsync(Guid messageId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var rowsUpdated = await _dbContext.TransactionalMessages
            .Where(m => m.Id == messageId && m.Status == OutboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                .SetProperty(m => m.ErrorMessage, errorMessage)
                .SetProperty(m => m.FailedAt, DateTimeOffset.UtcNow),
                cancellationToken);

        if (rowsUpdated == 0)
        {
            _logger.LogDebug("Message {MessageId} already processed or not in pending state", messageId);
            return;
        }

        // 第二步：加载最新状态以计算 NextTryAt 和最终状态
        var message = await _dbContext.TransactionalMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message == null)
        {
            return;
        }

        if (message.RetryCount >= _options.MaxRetryCount)
        {
            // 永久失败
            message.Status = OutboxMessageStatus.Failed;
            _logger.LogWarning(
                "Message {MessageId} marked as permanently failed after {RetryCount} retries. Error: {Error}",
                messageId, message.RetryCount, errorMessage);
        }
        else
        {
            var delay = TimeSpan.FromTicks((long)(_options.RetryDelayBase.Ticks * Math.Pow(2, message.RetryCount - 1))
            );

            // 应用上限
            if (delay > _options.MaxRetryDelay)
            {
                delay = _options.MaxRetryDelay;
            }

            message.NextTryAt = DateTimeOffset.UtcNow + delay;
            message.Status = OutboxMessageStatus.Pending;

            _logger.LogWarning(
                "Message {MessageId} scheduled for retry #{RetryCount} at {NextTryAt}. Error: {Error}",
                messageId, message.RetryCount, message.NextTryAt, errorMessage);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 直接标记为永久失败（用于反序列化错误等不可恢复场景）
    /// </summary>
    private async Task MarkAsPermanentlyFailedAsync(
        Guid messageId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.TransactionalMessages
            .Where(m => m.Id == messageId && m.Status != OutboxMessageStatus.Failed)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, OutboxMessageStatus.Failed)
                .SetProperty(m => m.ErrorMessage, reason)
                .SetProperty(m => m.FailedAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string Serialize(object obj)
    {
        return JsonSerializer.Serialize(obj, obj.GetType(), SerializeOptions);
    }

    private static T Deserialize<T>(string typeName, string payload)
    {
        var type = Type.GetType(typeName)
            ?? throw new InvalidOperationException($"Cannot resolve type: {typeName}");

        return (T)JsonSerializer.Deserialize(payload, type, SerializeOptions)!;
    }
}