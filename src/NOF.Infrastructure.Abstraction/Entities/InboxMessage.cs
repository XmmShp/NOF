namespace NOF.Infrastructure.Abstraction;

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
