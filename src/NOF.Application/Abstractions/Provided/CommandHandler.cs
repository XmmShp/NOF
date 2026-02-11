using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Non-generic marker interface for message handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IMessageHandler;

/// <summary>
/// Non-generic marker interface for command handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ICommandHandler : IMessageHandler;

/// <summary>
/// Handles commands of the specified type.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandHandler<TCommand> : ICommandHandler
    where TCommand : class, ICommand
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for command handlers, providing transactional message sending capabilities.
/// Works automatically via AsyncLocal without requiring any injected dependencies.
/// </summary>
public abstract class CommandHandler<TCommand> : HandlerBase, ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    /// <inheritdoc />
    public abstract Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}
