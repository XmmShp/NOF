namespace NOF;

public interface ICommandSender
{
    Task SendAsync(ICommand command, string? destinationEndpointName = null, CancellationToken cancellationToken = default);
}

public interface ISender : ICommandSender, IRequestSender;

public class Sender : ISender
{
    private readonly ICommandSender _commandSender;
    private readonly IRequestSender _requestSender;

    public Sender(ICommandSender commandSender, IRequestSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(commandSender);
        ArgumentNullException.ThrowIfNull(requestSender);

        _commandSender = commandSender;
        _requestSender = requestSender;
    }

    public Task SendAsync(ICommand command, string? destinationEndpointName, CancellationToken cancellationToken)
        => _commandSender.SendAsync(command, destinationEndpointName, cancellationToken);

    public Task<Result> SendAsync(IRequest request, string? destinationEndpointName, CancellationToken cancellationToken)
        => _requestSender.SendAsync(request, destinationEndpointName, cancellationToken);

    public Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, string? destinationEndpointName, CancellationToken cancellationToken)
        => _requestSender.SendAsync(request, destinationEndpointName, cancellationToken);
}