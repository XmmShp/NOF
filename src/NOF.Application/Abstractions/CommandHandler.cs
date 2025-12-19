using NOF.Application.Internals;

namespace NOF;

public interface ICommandHandler<TCommand> : ICommandHandler
    where TCommand : class, ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}