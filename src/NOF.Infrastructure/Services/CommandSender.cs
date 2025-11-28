using MassTransit;

namespace NOF;

public class CommandSender : ICommandSender
{
    private readonly IScopedClientFactory _clientFactory;
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public CommandSender(IScopedClientFactory clientFactory, ISendEndpointProvider sendEndpointProvider)
    {
        _clientFactory = clientFactory;
        _sendEndpointProvider = sendEndpointProvider;
    }

    #region CommandSender
    public Task<Result> SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand
        => SendAsync(command, command.GetQueueUri(), cancellationToken);

    public Task<Result> SendAsync<TCommand>(TCommand command, Uri destinationAddress, CancellationToken cancellationToken)
        where TCommand : class, ICommand
    {
        return SendCommandAsync<TCommand, Result>(command, destinationAddress, cancellationToken);
    }
    #endregion

    #region Command<T>Sender
    public Task<Result<TResponse>> SendAsync<TCommand, TResponse>(TCommand command, Uri destinationAddress, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TResponse>
    {
        return SendCommandAsync<TCommand, Result<TResponse>>(command, command.GetQueueUri(), cancellationToken);
    }

    public Task<Result<TResponse>> SendAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TResponse>
        => SendAsync<TCommand, TResponse>(command, command.GetQueueUri(), cancellationToken);
    #endregion

    #region AsyncCommandSender
    public Task SendAsync(IAsyncCommand command, CancellationToken cancellationToken = default)
        => SendAsync(command, command.GetQueueUri(), cancellationToken);

    public Task SendAsync(IAsyncCommand command, Uri destinationAddress, CancellationToken cancellationToken = default)
    {
        return _sendEndpointProvider.SendCommand(command, destinationAddress, cancellationToken);
    }
    #endregion

    private async Task<TResult> SendCommandAsync<TCommand, TResult>(TCommand command, Uri destinationAddress, CancellationToken cancellationToken)
        where TCommand : class
        where TResult : class
    {
        var requestHandle = _clientFactory.CreateRequest(destinationAddress, command, cancellationToken);
        var response = await requestHandle.GetResponse<TResult>();
        return response.Message;
    }
}