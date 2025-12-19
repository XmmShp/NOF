namespace NOF;

public interface ICommandSender
{
    Task SendAsync(ICommand command, string? destinationEndpointName = null, CancellationToken cancellationToken = default);
}