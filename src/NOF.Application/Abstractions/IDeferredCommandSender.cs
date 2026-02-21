using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Deferred command sender for adding commands to the transactional outbox context.
/// Commands will be persisted to the outbox when UnitOfWork.SaveChangesAsync is called.
/// </summary>
public interface IDeferredCommandSender
{
    /// <summary>
    /// Adds a command to the transactional outbox context.
    /// The command will be persisted to the outbox when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    void Send(ICommand command, string? destinationEndpointName = null);
}
