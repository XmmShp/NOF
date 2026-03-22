using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Domain;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryOutboxMessageRepository : MemoryRepository<NOFOutboxMessage, long>, IOutboxMessageRepository
{
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<MemoryOutboxMessageRepository> _logger;
    private readonly IIdGenerator _idGenerator;

    public MemoryOutboxMessageRepository(
        MemoryPersistenceStore store,
        MemoryPersistenceSession session,
        IInvocationContext invocationContext,
        IOptions<OutboxOptions> options,
        ILogger<MemoryOutboxMessageRepository> logger,
        IIdGenerator idGenerator)
        : base(
            store,
            session,
            () => $"nof:outbox:{MemoryPersistenceStore.NormalizeTenantId(invocationContext.TenantId)}",
            static message => message.Id,
            static message => new NOFOutboxMessage
            {
                Id = message.Id,
                DestinationEndpointName = message.DestinationEndpointName,
                CreatedAt = message.CreatedAt,
                RetryCount = message.RetryCount,
                MessageType = message.MessageType,
                PayloadType = message.PayloadType,
                Payload = message.Payload,
                Headers = message.Headers,
                SentAt = message.SentAt,
                FailedAt = message.FailedAt,
                ErrorMessage = message.ErrorMessage,
                ClaimedBy = message.ClaimedBy,
                ClaimExpiresAt = message.ClaimExpiresAt,
                Status = message.Status,
                TraceId = message.TraceId,
                SpanId = message.SpanId
            })
    {
        _options = options;
        _logger = logger;
        _idGenerator = idGenerator;
    }

    public override void Add(NOFOutboxMessage entity)
    {
        if (entity.Id == 0)
        {
            entity.Id = _idGenerator.NextId();
        }

        entity.Status = OutboxMessageStatus.Pending;
        entity.ClaimedBy = null;
        entity.ClaimExpiresAt = null;
        base.Add(entity);
    }

    public async IAsyncEnumerable<NOFOutboxMessage> AtomicClaimPendingMessagesAsync(int batchSize = 100, TimeSpan? claimTimeout = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var timeout = claimTimeout ?? _options.Value.ClaimTimeout;
        var claimedBy = Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.UtcNow.Add(timeout);
        List<NOFOutboxMessage> claimed;

        lock (Store.SyncRoot)
        {
            claimed = Items.Values
                .Where(m => m.Status == OutboxMessageStatus.Pending &&
                            m.RetryCount < _options.Value.MaxRetryCount &&
                            (m.ClaimExpiresAt is null || m.ClaimExpiresAt <= DateTime.UtcNow))
                .OrderBy(m => m.CreatedAt)
                .Take(batchSize)
                .Select(message =>
                {
                    message.RetryCount++;
                    message.ClaimedBy = claimedBy;
                    message.ClaimExpiresAt = expiresAt;
                    return Store.CloneEntity(message);
                })
                .ToList();
        }

        foreach (var message in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return message;
            await Task.CompletedTask;
        }
    }

    public ValueTask AtomicMarkAsSentAsync(IEnumerable<long> messageIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (Store.SyncRoot)
        {
            foreach (var messageId in messageIds)
            {
                if (!Items.TryGetValue(messageId, out var message) || message.Status != OutboxMessageStatus.Pending)
                {
                    continue;
                }

                message.Status = OutboxMessageStatus.Sent;
                message.SentAt = DateTime.UtcNow;
                message.ClaimedBy = null;
                message.ClaimExpiresAt = null;
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask AtomicRecordDeliveryFailureAsync(long messageId, string errorMessage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (Store.SyncRoot)
        {
            if (!Items.TryGetValue(messageId, out var message) || message.Status != OutboxMessageStatus.Pending)
            {
                _logger.LogDebug("Message {MessageId} already processed or not in pending state", messageId);
                return ValueTask.CompletedTask;
            }

            message.ErrorMessage = errorMessage;
            message.FailedAt = DateTime.UtcNow;

            if (message.RetryCount >= _options.Value.MaxRetryCount)
            {
                message.Status = OutboxMessageStatus.Failed;
                message.ClaimedBy = null;
                message.ClaimExpiresAt = null;
                _logger.LogWarning("Message {MessageId} marked as permanently failed after {RetryCount} retries. Error: {Error}", messageId, message.RetryCount, errorMessage);
            }
            else
            {
                message.Status = OutboxMessageStatus.Pending;
                message.ClaimedBy = null;
                message.ClaimExpiresAt = null;
                _logger.LogWarning("Message {MessageId} scheduled for retry #{RetryCount}. Error: {Error}", messageId, message.RetryCount, errorMessage);
            }
        }

        return ValueTask.CompletedTask;
    }

    protected override IEnumerable<NOFOutboxMessage> OrderItems(IEnumerable<NOFOutboxMessage> items)
        => items.OrderBy(message => message.CreatedAt);
}
