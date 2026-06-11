using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Sends command messages.
/// </summary>
public interface ICommandSender
{
    /// <summary>
    /// Adds a command to the transactional outbox context.
    /// The command will be persisted to the outbox when the active <see cref="Microsoft.EntityFrameworkCore.DbContext"/> saves changes.
    /// </summary>
    void DeferSend(object command, Type commandType);

    /// <summary>Sends a command.</summary>
    Task SendAsync(object command, Type commandType, Context context, CancellationToken cancellationToken = default);
}

public static class CommandSenderExtensions
{
    extension(ICommandSender sender)
    {
        public void DeferSend<TCommand>(TCommand command)
        {
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(command);
            sender.DeferSend(command, typeof(TCommand));
        }

        public Task SendAsync<TCommand>(TCommand command, Context context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(context);
            return sender.SendAsync(command, typeof(TCommand), context, cancellationToken);
        }
    }
}
