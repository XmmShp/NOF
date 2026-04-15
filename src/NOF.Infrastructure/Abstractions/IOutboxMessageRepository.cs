namespace NOF.Infrastructure;

/// <summary>
/// Outbox message repository interface.
/// Supports transactional adds that participate in the current persistence context,
/// plus atomic operations used by background delivery workflows.
/// </summary>
public interface IOutboxMessageRepository
{
    void Add(NOFOutboxMessage message);

    IAsyncEnumerable<NOFOutboxMessage> AtomicClaimPendingMessagesAsync(int batchSize = 100, TimeSpan? claimTimeout = null, CancellationToken cancellationToken = default);

    ValueTask AtomicMarkAsSentAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken = default);

    ValueTask AtomicRecordDeliveryFailureAsync(Guid messageId, string errorMessage, CancellationToken cancellationToken = default);
}
