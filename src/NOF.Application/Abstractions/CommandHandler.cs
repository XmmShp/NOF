using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Base type for message handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class MessageHandler;

/// <summary>
/// Non-generic base type for command handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class CommandHandler : MessageHandler
{
    public abstract Task HandleAsync(ICommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Handles commands of the specified type.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public abstract class CommandHandler<TCommand> : CommandHandler
    where TCommand : class, ICommand
{
    /// <inheritdoc />
    public sealed override Task HandleAsync(ICommand command, CancellationToken cancellationToken)
        => HandleAsync((TCommand)command, cancellationToken);

    /// <summary>Handles the command.</summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public abstract Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}
