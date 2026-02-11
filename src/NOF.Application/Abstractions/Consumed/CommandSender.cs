using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Sends command messages to their destination endpoints.
/// </summary>
public interface ICommandSender
{
    /// <summary>Sends a command asynchronously.</summary>
    /// <param name="command">The command to send.</param>
    /// <param name="destinationEndpointName">Optional destination endpoint name override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(ICommand command, string? destinationEndpointName = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Deferred command sender for manually adding commands to the transactional outbox context
/// without using HandlerBase.
/// </summary>
public interface IDeferredCommandSender
{
    /// <summary>
    /// Adds a command to the transactional outbox context.
    /// The command will be persisted to the outbox when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    void Send(ICommand command, string? destinationEndpointName = null);
}
