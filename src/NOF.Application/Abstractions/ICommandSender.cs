using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Sends command messages.
/// </summary>
public interface ICommandSender
{
    /// <summary>Sends a command.</summary>
    Task SendAsync(ICommand command, CancellationToken cancellationToken = default);
}
