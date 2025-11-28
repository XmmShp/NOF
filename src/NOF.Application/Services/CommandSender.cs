namespace NOF;

public interface ICommandSender
{
    Task<Result> SendAsync<TCommand>(TCommand command, Uri destinationAddress, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand;

    Task<Result> SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand;

    Task<Result<TResponse>> SendAsync<TCommand, TResponse>(TCommand command, Uri destinationAddress, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TResponse>;

    Task<Result<TResponse>> SendAsync<TCommand, TResponse>(TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TResponse>;

    Task SendAsync(IAsyncCommand command, Uri destinationAddress, CancellationToken cancellationToken = default);
    Task SendAsync(IAsyncCommand command, CancellationToken cancellationToken = default);
}