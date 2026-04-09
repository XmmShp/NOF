using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Sends command messages.
/// </summary>
public interface ICommandSender
{
    /// <summary>
    /// Adds a command to the transactional outbox context.
    /// The command will be persisted to the outbox when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    void DeferSend(ICommand command);

    /// <summary>Sends a command.</summary>
    Task SendAsync(ICommand command, CancellationToken cancellationToken = default);
}
