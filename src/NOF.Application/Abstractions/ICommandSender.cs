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
    Task DeferSendAsync(object command, Type commandType, Context context, CancellationToken cancellationToken = default);

    /// <summary>Sends a command.</summary>
    Task SendAsync(object command, Type commandType, Context context, CancellationToken cancellationToken = default);
}

public static class CommandSenderExtensions
{
    extension(ICommandSender sender)
    {
        public Task DeferSendAsync<TCommand>(TCommand command, Context context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(context);
            return sender.DeferSendAsync(command, typeof(TCommand), context, cancellationToken);
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
