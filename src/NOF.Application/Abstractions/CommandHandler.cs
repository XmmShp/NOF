namespace NOF;

public interface ICommandHandler;

public interface ICommandHandler<TCommand> : ICommandHandler
    where TCommand : class, ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}