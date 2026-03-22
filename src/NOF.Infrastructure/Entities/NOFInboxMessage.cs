using NOF.Domain;

namespace NOF.Infrastructure;

/// <summary>
/// Inbox message entity used for tracking reliably processed messages.
/// </summary>
public class NOFInboxMessage : AggregateRoot, ICloneable
{
    /// <summary>
    /// The unique message identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The message creation time.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public NOFInboxMessage(Guid id)
    {
        Id = id;
    }

    public object Clone()
        => new NOFInboxMessage(Id)
        {
            CreatedAt = CreatedAt
        };
}
