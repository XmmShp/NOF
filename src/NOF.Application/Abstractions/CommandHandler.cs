namespace NOF;

public interface ICommandHandler;

public interface IAsyncCommandHandler<TCommand> : ICommandHandler
    where TCommand : class, IAsyncCommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<TCommand> : ICommandHandler
    where TCommand : class, ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<TCommand, TResponse> : ICommandHandler
    where TCommand : class, ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}