namespace NOF.Infrastructure.Core;

/// <summary>
/// Inbox message entity used for tracking reliably processed messages.
/// </summary>
public class InboxMessage
{
    /// <summary>
    /// The unique message identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The message creation time.
    /// </summary>
    public DateTime CreatedAt { get; }

    public InboxMessage(Guid id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Inbox message repository interface responsible for persisting inbox messages.
/// </summary>
public interface IInboxMessageRepository
{
    /// <summary>
    /// Adds an inbox message.
    /// </summary>
    /// <param name="message">The inbox message.</param>
    void Add(InboxMessage message);

    /// <summary>
    /// Checks whether a message exists by its ID.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message exists; otherwise false.</returns>
    Task<bool> ExistByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default);
}
