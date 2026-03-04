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
public interface ICommandHandler : IMessageHandler
{
    Task HandleAsync(ICommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Handles commands of the specified type.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandHandler<in TCommand> : ICommandHandler
    where TCommand : class, ICommand
{
    Task ICommandHandler.HandleAsync(ICommand command, CancellationToken cancellationToken)
        => HandleAsync((TCommand)command, cancellationToken);

    /// <summary>Handles the command.</summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}

