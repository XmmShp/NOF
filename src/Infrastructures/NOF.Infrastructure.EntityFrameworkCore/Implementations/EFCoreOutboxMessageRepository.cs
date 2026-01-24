using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Contract.Annotations;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF;

internal sealed class EFCoreOutboxMessageRepository : IOutboxMessageRepository
{
    private readonly NOFPublicDbContext _dbContext;
    private readonly OutboxOptions _options;
    private readonly ILogger<EFCoreOutboxMessageRepository> _logger;

    public EFCoreOutboxMessageRepository(
        NOFPublicDbContext dbContext,
        IOptions<OutboxOptions> options,
        ILogger<EFCoreOutboxMessageRepository> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public void Add(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default)
    {
        var outboxMessages = new List<EFCoreOutboxMessage>();

        foreach (var msg in messages)
        {
            var messageType = msg.Message is ICommand
                ? OutboxMessageType.Command
                : OutboxMessageType.Notification;

            outboxMessages.Add(new EFCoreOutboxMessage
            {
                Id = msg.Id,
                MessageType = messageType,
                PayloadType = msg.Message.GetType().AssemblyQualifiedName!,
                Payload = Serialize(msg.Message),
                Headers = Serialize(msg.Headers),
                DestinationEndpointName = msg.DestinationEndpointName,
                CreatedAt = msg.CreatedAt,
                Status = OutboxMessageStatus.Pending,
                RetryCount = msg.RetryCount,
                ClaimedBy = null,
                ClaimExpiresAt = null,
                TraceId = msg.TraceId?.ToString(),
                SpanId = msg.SpanId?.ToString()
            });
        }

        if (outboxMessages.Count > 0)
        {
            _dbContext.OutboxMessages.AddRange(outboxMessages);
            _logger.LogDebug("Added {Count} messages to outbox", outboxMessages.Count);
        }
    }

    /// <summary>
    /// 抢占式获取待发送的消息，避免多实例重复处理
    /// </summary>
    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingMessagesAsync(
        int batchSize,
        TimeSpan? claimTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var timeout = claimTimeout ?? _options.ClaimTimeout;
        var lockId = Guid.NewGuid().ToString();
        var expiresAt = DateTimeOffset.UtcNow.Add(timeout);

        int rowsUpdated;
        // 第一步：直接声明待处理消息（包括过期锁的消息）
        try
        {
            rowsUpdated = await _dbContext.OutboxMessages
                .Where(m => m.Status == OutboxMessageStatus.Pending &&
                            m.RetryCount < _options.MaxRetryCount &&
                            (m.ClaimExpiresAt == null || m.ClaimExpiresAt <= DateTimeOffset.UtcNow))
                .OrderBy(m => m.CreatedAt)
                .Take(batchSize)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                        .SetProperty(m => m.ClaimedBy, lockId)
                        .SetProperty(m => m.ClaimExpiresAt, expiresAt),
                    cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new List<OutboxMessage>();
        }

        if (rowsUpdated == 0)
        {
            return new List<OutboxMessage>();
        }

        // 第二步：获取成功声明的消息
        var claimedMessages = await _dbContext.OutboxMessages
            .Where(m => m.ClaimedBy == lockId)
            .ToListAsync(cancellationToken);

        var result = new List<OutboxMessage>(claimedMessages.Count);

        foreach (var m in claimedMessages)
        {
            try
            {
                var message = Deserialize<IMessage>(m.PayloadType, m.Payload);

                result.Add(new OutboxMessage
                {
                    Id = m.Id,
                    Message = message,
                    Headers = Deserialize<Dictionary<string, string?>>(typeof(Dictionary<string, string?>).AssemblyQualifiedName!, m.Headers),
                    DestinationEndpointName = m.DestinationEndpointName,
                    CreatedAt = m.CreatedAt,
                    RetryCount = m.RetryCount,
                    TraceId = string.IsNullOrEmpty(m.TraceId) ? null : ActivityTraceId.CreateFromString(m.TraceId),
                    SpanId = string.IsNullOrEmpty(m.SpanId) ? null : ActivitySpanId.CreateFromString(m.SpanId)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialization failed for claimed message {MessageId}, type: {TypeName}",
                    m.Id, m.PayloadType);

                // 释放失败消息的锁并标记为永久失败
                await ReleaseClaimAndMarkAsFailedAsync(m.Id, $"Deserialization error: {ex.Message}", cancellationToken);
            }
        }

        _logger.LogDebug("Successfully claimed {ClaimedCount} messages with lock {LockId}",
            result.Count, lockId);

        return result;
    }

    public async Task MarkAsSentAsync(
        IEnumerable<long> messageIds,
        CancellationToken cancellationToken = default)
    {
        // 避免重复标记（如已被其他实例处理）
        await _dbContext.OutboxMessages
            .Where(m => messageIds.Contains(m.Id) && m.Status == OutboxMessageStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxMessageStatus.Sent)
                .SetProperty(m => m.SentAt, DateTimeOffset.UtcNow)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAt, (DateTimeOffset?)null),
                cancellationToken);
    }

    /// <summary>
    /// 记录发送失败，并根据重试次数决定是否继续重试或永久失败。
    /// </summary>
    public async Task RecordDeliveryFailureAsync(long messageId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var rowsUpdated = await _dbContext.OutboxMessages
            .Where(m => m.Id == messageId && m.Status == OutboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.ErrorMessage, errorMessage)
                .SetProperty(m => m.FailedAt, DateTimeOffset.UtcNow),
                cancellationToken);

        if (rowsUpdated == 0)
        {
            _logger.LogDebug("Message {MessageId} already processed or not in pending state", messageId);
            return;
        }

        // 第二步：加载最新状态以计算最终状态
        var message = await _dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message == null)
        {
            return;
        }

        if (message.RetryCount >= _options.MaxRetryCount)
        {
            // 永久失败
            message.Status = OutboxMessageStatus.Failed;
            message.ClaimedBy = null;
            message.ClaimExpiresAt = null;
            _logger.LogWarning(
                "Message {MessageId} marked as permanently failed after {RetryCount} retries. Error: {Error}",
                messageId, message.RetryCount, errorMessage);
        }
        else
        {
            message.Status = OutboxMessageStatus.Pending;
            message.ClaimedBy = null;
            message.ClaimExpiresAt = null;

            _logger.LogWarning(
                "Message {MessageId} scheduled for retry #{RetryCount}. Error: {Error}",
                messageId, message.RetryCount, errorMessage);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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

    /// <summary>
    /// 释放抢占锁并标记消息为失败状态
    /// </summary>
    private async Task ReleaseClaimAndMarkAsFailedAsync(
        long messageId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, OutboxMessageStatus.Failed)
                .SetProperty(m => m.ErrorMessage, errorMessage)
                .SetProperty(m => m.FailedAt, DateTimeOffset.UtcNow)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAt, (DateTimeOffset?)null),
                cancellationToken);
    }
}