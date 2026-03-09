using NOF.Domain;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Inbox message repository interface responsible for persisting inbox messages.
/// </summary>
public interface IInboxMessageRepository : IRepository<NOFInboxMessage, Guid>
{
    /// <summary>
    /// Checks whether a message exists by its ID.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message exists; otherwise false.</returns>
    Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default);
}
