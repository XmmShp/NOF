using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Contract;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal sealed class EFCoreOutboxMessageRepository : EFCoreRepository<NOFOutboxMessage>, IOutboxMessageRepository
{
    private readonly NOFDbContext _dbContext;
    private readonly OutboxOptions _options;
    private readonly ILogger<EFCoreOutboxMessageRepository> _logger;
    private readonly IMessageSerializer _messageSerializer;

    public EFCoreOutboxMessageRepository(
        NOFDbContext dbContext,
        IOptions<OutboxOptions> options,
        ILogger<EFCoreOutboxMessageRepository> logger,
        IMessageSerializer messageSerializer) : base(dbContext)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
        _messageSerializer = messageSerializer;
    }

    public override void Add(NOFOutboxMessage aggregateRoot)
    {
        aggregateRoot.Status = OutboxMessageStatus.Pending;
        aggregateRoot.ClaimedBy = null;
        aggregateRoot.ClaimExpiresAt = null;
        base.Add(aggregateRoot);
    }

    /// <summary>
    /// Claims pending messages for delivery, preventing duplicate processing across instances.
    /// </summary>
    public async IAsyncEnumerable<NOFOutboxMessage> AtomicClaimPendingMessagesAsync(
        int batchSize,
        TimeSpan? claimTimeout = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var timeout = claimTimeout ?? _options.ClaimTimeout;
        var lockId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(timeout);

        int rowsUpdated;
        // Step 1: Claim pending messages (including those with expired locks).
        // Some providers don't translate ExecuteUpdate with OrderBy/Take. Use a two-step approach: select IDs then update by IN.
        var toClaimIds = await _dbContext.NOFOutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending &&
                        m.RetryCount < _options.MaxRetryCount &&
                        (m.ClaimExpiresAt == null || m.ClaimExpiresAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (toClaimIds.Count == 0)
        {
            yield break;
        }

        rowsUpdated = await _dbContext.NOFOutboxMessages
            .Where(m => toClaimIds.Contains(m.Id))
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                    .SetProperty(m => m.ClaimedBy, lockId)
                    .SetProperty(m => m.ClaimExpiresAt, expiresAt),
                cancellationToken);

        if (rowsUpdated == 0)
        {
            yield break;
        }

        // Step 2: Retrieve successfully claimed messages
        var claimedMessages = await _dbContext.NOFOutboxMessages
            .Where(m => m.ClaimedBy == lockId)
            .ToListAsync(cancellationToken);

        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));

        foreach (var message in claimedMessages)
        {
            NOFOutboxMessage claimedMessage;
            try
            {
                var deserializedMessage = _messageSerializer.Deserialize(message.PayloadType, message.Payload);
                var headers = string.IsNullOrWhiteSpace(message.Headers)
                    ? new Dictionary<string, string?>()
                    : JsonSerializer.Deserialize(message.Headers, headersTypeInfo) ?? new Dictionary<string, string?>();

                claimedMessage = new NOFOutboxMessage
                {
                    Id = message.Id,
                    MessageType = deserializedMessage is ICommand ? OutboxMessageType.Command : OutboxMessageType.Notification,
                    PayloadType = message.PayloadType,
                    Payload = message.Payload,
                    Headers = JsonSerializer.Serialize(headers, headersTypeInfo),
                    CreatedAt = message.CreatedAt,
                    RetryCount = message.RetryCount,
                    ParentTracingInfo = message.ParentTracingInfo is null ? null : new TracingInfo(message.ParentTracingInfo.TraceId, message.ParentTracingInfo.SpanId),
                    Status = message.Status,
                    ClaimedBy = message.ClaimedBy,
                    ClaimExpiresAt = message.ClaimExpiresAt,
                    SentAt = message.SentAt,
                    FailedAt = message.FailedAt,
                    ErrorMessage = message.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialization failed for claimed message {MessageId}, type: {TypeName}",
                    message.Id, message.PayloadType);

                await ReleaseClaimAndMarkAsFailedAsync(message.Id, $"Deserialization error: {ex.Message}", cancellationToken);
                continue;
            }

            yield return claimedMessage;
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

        // Step 2: Load the latest state to determine the final status
        var message = await _dbContext.NOFOutboxMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message == null)
        {
            return;
        }

        if (message.RetryCount >= _options.MaxRetryCount)
        {
            // Permanently failed
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
