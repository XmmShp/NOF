using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Sends command messages to their destination endpoints.
/// </summary>
public interface ICommandSender
{
    /// <summary>Sends a command with headers and destination.</summary>
    Task SendAsync(ICommand command, IDictionary<string, string?>? headers, string? destinationEndpointName, CancellationToken cancellationToken = default);

    /// <summary>Sends a command.</summary>
    Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
        => SendAsync(command, null, null, cancellationToken);

    /// <summary>Sends a command with extra headers.</summary>
    Task SendAsync(ICommand command, IDictionary<string, string?> headers, CancellationToken cancellationToken = default)
        => SendAsync(command, headers, null, cancellationToken);

    /// <summary>Sends a command to a specific destination.</summary>
    Task SendAsync(ICommand command, string destinationEndpointName, CancellationToken cancellationToken = default)
        => SendAsync(command, null, destinationEndpointName, cancellationToken);
}

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
