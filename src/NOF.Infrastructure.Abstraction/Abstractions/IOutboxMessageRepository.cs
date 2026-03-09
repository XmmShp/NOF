using NOF.Domain;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Outbox message repository interface.
/// Supports transactional adds that participate in the current persistence context,
/// plus atomic operations used by background delivery workflows outside <see cref="NOF.Application.IUnitOfWork"/>.
/// </summary>
public interface IOutboxMessageRepository : IRepository<NOFOutboxMessage, long>
{
    /// <summary>
    /// Atomically claims pending messages for delivery, preventing duplicate processing across instances.
    /// Returned messages are marked as in-progress and cannot be claimed by other instances.
    /// </summary>
    IAsyncEnumerable<NOFOutboxMessage> AtomicClaimPendingMessagesAsync(int batchSize = 100, TimeSpan? claimTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically marks messages as sent without going through <see cref="NOF.Application.IUnitOfWork"/>.
    /// </summary>
    ValueTask AtomicMarkAsSentAsync(IEnumerable<long> messageIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically records a delivery failure for a message without going through <see cref="NOF.Application.IUnitOfWork"/>.
    /// </summary>
    ValueTask AtomicRecordDeliveryFailureAsync(long messageId, string errorMessage, CancellationToken cancellationToken = default);
}
