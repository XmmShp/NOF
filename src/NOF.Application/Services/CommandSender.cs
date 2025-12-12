namespace NOF;

public interface ICommandSender
{
    Task<Result> SendAsync(ICommand command, string? destinationEndpointName = null, CancellationToken cancellationToken = default);
    Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, string? destinationEndpointName = null, CancellationToken cancellationToken = default);
    Task SendAsync(IAsyncCommand command, string? destinationEndpointName = null, CancellationToken cancellationToken = default);
}

public interface ISender : ICommandSender, IRequestSender;

public class Sender : ISender
{
    private readonly ICommandSender _commandSender;
    private readonly IRequestSender _requestSender;

    public Sender(ICommandSender commandSender, IRequestSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(commandSender);
        _commandSender = commandSender;

        ArgumentNullException.ThrowIfNull(requestSender);
        _requestSender = requestSender;
    }

    public Task<Result> SendAsync(ICommand command, string? destinationEndpointName = null, CancellationToken cancellationToken = default)
        => _commandSender.SendAsync(command, destinationEndpointName, cancellationToken);

    public Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, string? destinationEndpointName = null, CancellationToken cancellationToken = default)
        => _commandSender.SendAsync(command, destinationEndpointName, cancellationToken);

    public Task SendAsync(IAsyncCommand command, string? destinationEndpointName = null, CancellationToken cancellationToken = default)
        => _commandSender.SendAsync(command, destinationEndpointName, cancellationToken);

    public Task<Result> SendAsync(IRequest request, CancellationToken cancellationToken = default)
        => _requestSender.SendAsync(request, cancellationToken);

    public Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        => _requestSender.SendAsync(request, cancellationToken);
}