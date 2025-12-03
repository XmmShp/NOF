namespace NOF;

public interface ICommandSender
{
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default);
    Task<Result> SendAsync(ICommand command, Uri destinationAddress, CancellationToken cancellationToken = default);

    Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, Uri destinationAddress, CancellationToken cancellationToken = default);
    Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    Task SendAsync(IAsyncCommand command, Uri destinationAddress, CancellationToken cancellationToken = default);
    Task SendAsync(IAsyncCommand command, CancellationToken cancellationToken = default);
}