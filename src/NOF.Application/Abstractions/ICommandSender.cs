using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Sends command messages.
/// </summary>
public interface ICommandSender
{
    /// <summary>Sends a command with headers.</summary>
    Task SendAsync(ICommand command, IDictionary<string, string?>? headers, CancellationToken cancellationToken = default);

    /// <summary>Sends a command.</summary>
    Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
        => SendAsync(command, null, cancellationToken);
}
