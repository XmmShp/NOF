namespace NOF.Infrastructure.Core;

/// <summary>
/// Transactional outbox message repository interface.
/// Operates on outbox messages within a transactional context.
/// Infrastructure layers must implement this interface to provide:
/// 1. Persistence (outbox table)
/// 2. Reliable delivery (background service)
/// </summary>
public interface IOutboxMessageRepository
{
    /// <summary>
    /// Adds messages to the outbox within the current transactional context.
    /// </summary>
    void Add(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims pending messages for delivery, preventing duplicate processing across instances.
    /// Returned messages are marked as in-progress and cannot be claimed by other instances.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimPendingMessagesAsync(int batchSize = 100, TimeSpan? claimTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks messages as sent.
    /// </summary>
    Task MarkAsSentAsync(IEnumerable<long> messageIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a delivery failure for a message.
    /// </summary>
    Task RecordDeliveryFailureAsync(long messageId, string errorMessage, CancellationToken cancellationToken = default);
}
