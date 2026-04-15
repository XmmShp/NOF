using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal sealed class EFCoreOutboxMessageRepository : IOutboxMessageRepository
{
    private readonly NOFDbContext _dbContext;
    private readonly OutboxOptions _options;
    private readonly ILogger<EFCoreOutboxMessageRepository> _logger;

    public EFCoreOutboxMessageRepository(
        NOFDbContext dbContext,
        IOptions<OutboxOptions> options,
        ILogger<EFCoreOutboxMessageRepository> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public void Add(NOFOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        message.Status = OutboxMessageStatus.Pending;
        message.ClaimedBy = null;
        message.ClaimExpiresAt = null;
        _dbContext.Set<NOFOutboxMessage>().Add(message);
    }

    /// <summary>
    /// Claims pending messages for delivery, preventing duplicate processing across instances.
    /// </summary>
    public async IAsyncEnumerable<NOFOutboxMessage> AtomicClaimPendingMessagesAsync(
        int batchSize,
        TimeSpan? claimTimeout = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            batchSize = _options.BatchSize;
        }
        var timeout = claimTimeout ?? _options.ClaimTimeout;
        var lockId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(timeout);

        int rowsUpdated;
        var maxRetry = _options.MaxRetryCount;

        rowsUpdated = await _dbContext.NOFOutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending &&
                        m.RetryCount < maxRetry &&
                        (m.ClaimedBy == null || m.ClaimExpiresAt == null || m.ClaimExpiresAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                    .SetProperty(m => m.ClaimedBy, lockId)
                    .SetProperty(m => m.ClaimExpiresAt, expiresAt),
                cancellationToken);

        if (rowsUpdated == 0)
        {
            yield break;
        }

        var claimedMessagesFromDb = await _dbContext.NOFOutboxMessages
            .AsNoTracking()
            .Where(m => m.ClaimedBy == lockId)
            .ToListAsync(cancellationToken);

        foreach (var msgFromDb in claimedMessagesFromDb)
        {
            var trackedEntry = _dbContext.ChangeTracker.Entries<NOFOutboxMessage>()
                .FirstOrDefault(e => e.Entity.Id == msgFromDb.Id);

            if (trackedEntry != null)
            {
                await trackedEntry.ReloadAsync(cancellationToken);
                yield return trackedEntry.Entity;
            }
            else
            {
                _dbContext.Attach(msgFromDb);
                yield return msgFromDb;
            }
        }
    }

    public async ValueTask AtomicMarkAsSentAsync(
        IEnumerable<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        var sentAt = DateTime.UtcNow;

        await _dbContext.NOFOutboxMessages
            .Where(m => messageIds.Contains(m.Id) && m.Status == OutboxMessageStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxMessageStatus.Sent)
                .SetProperty(m => m.SentAt, sentAt)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAt, (DateTime?)null),
                cancellationToken);
    }

    /// <summary>
    /// Records a delivery failure and determines whether to retry or permanently fail based on retry count.
    /// </summary>
    public async ValueTask AtomicRecordDeliveryFailureAsync(Guid messageId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var failedAt = DateTime.UtcNow;
        var rowsUpdated = await _dbContext.NOFOutboxMessages
            .Where(m => m.Id == messageId && m.Status == OutboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.ErrorMessage, errorMessage)
                .SetProperty(m => m.FailedAt, failedAt),
                cancellationToken);

        if (rowsUpdated == 0)
        {
            _logger.LogDebug("Message {MessageId} already processed or not in pending state", messageId);
            return;
        }

        NOFOutboxMessage? message;
        var trackedEntry = _dbContext.ChangeTracker.Entries<NOFOutboxMessage>().FirstOrDefault(e => e.Entity.Id == messageId);
        if (trackedEntry != null)
        {
            await trackedEntry.ReloadAsync(cancellationToken);
            message = trackedEntry.Entity;
        }
        else
        {
            message = await _dbContext.NOFOutboxMessages
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        }

        if (message == null)
        {
            return;
        }

        if (message.RetryCount >= _options.MaxRetryCount)
        {
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

    /// <summary>
    /// Releases the claim lock and marks the message as failed.
    /// </summary>
    private async Task ReleaseClaimAndMarkAsFailedAsync(
        Guid messageId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var releaseFailedAt = DateTime.UtcNow;
        await _dbContext.NOFOutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, OutboxMessageStatus.Failed)
                .SetProperty(m => m.ErrorMessage, errorMessage)
                .SetProperty(m => m.FailedAt, releaseFailedAt)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAt, (DateTime?)null),
                cancellationToken);
    }
}
