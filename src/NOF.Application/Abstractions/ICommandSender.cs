using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Sends command messages.
/// </summary>
public interface ICommandSender
{
    /// <summary>
    /// Adds a command to the transactional outbox context.
    /// The command will be persisted to the outbox when the active <see cref="IDbContext"/> saves changes.
    /// </summary>
    Task DeferSendAsync(object command, Type commandType, Context context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an ordered command to the transactional outbox context.
    /// The framework qualifies <paramref name="orderKey"/> with the producer service name and assigns its sequence transactionally.
    /// Every command in one ordered stream must use the same key and reach each participating consumer handler without filtering gaps.
    /// Set <paramref name="completesOrderKey"/> only on the final command in the stream.
    /// </summary>
    Task DeferSendOrderedAsync(
        object command,
        Type commandType,
        string orderKey,
        Context context,
        bool completesOrderKey = false,
        CancellationToken cancellationToken = default);

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

        public Task DeferSendOrderedAsync<TCommand>(
            TCommand command,
            string orderKey,
            Context context,
            bool completesOrderKey = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(command);
            ArgumentException.ThrowIfNullOrWhiteSpace(orderKey);
            ArgumentNullException.ThrowIfNull(context);
            return sender.DeferSendOrderedAsync(
                command,
                typeof(TCommand),
                orderKey,
                context,
                completesOrderKey,
                cancellationToken);
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
