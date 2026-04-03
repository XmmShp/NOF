using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Domain;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryOutboxMessageRepository : MemoryRepository<NOFOutboxMessage, Guid>, IOutboxMessageRepository
{
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<MemoryOutboxMessageRepository> _logger;

    public MemoryOutboxMessageRepository(
        MemoryPersistenceContext context,
        IOptions<OutboxOptions> options,
        ILogger<MemoryOutboxMessageRepository> logger)
        : base(
            context,
            static message => message.Id)
    {
        _options = options;
        _logger = logger;
    }

    public override void Add(NOFOutboxMessage entity)
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
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
        var claimed = Context.Set<NOFOutboxMessage>()
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
                return (NOFOutboxMessage)message.Clone();
            })
            .ToList();

        foreach (var message in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return message;
            await Task.CompletedTask;
        }
    }

    public ValueTask AtomicMarkAsSentAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var messageId in messageIds)
        {
            var message = Context.Set<NOFOutboxMessage>().FirstOrDefault(m => m.Id == messageId);
            if (message is null || message.Status != OutboxMessageStatus.Pending)
            {
                continue;
            }

            message.Status = OutboxMessageStatus.Sent;
            message.SentAt = DateTime.UtcNow;
            message.ClaimedBy = null;
            message.ClaimExpiresAt = null;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask AtomicRecordDeliveryFailureAsync(Guid messageId, string errorMessage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var message = Context.Set<NOFOutboxMessage>().FirstOrDefault(m => m.Id == messageId);
        if (message is null || message.Status != OutboxMessageStatus.Pending)
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

        return ValueTask.CompletedTask;
    }
}
