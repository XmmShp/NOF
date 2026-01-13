using MassTransit;

namespace NOF;

public class MassTransitCommandSender : ICommandSender
{
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public MassTransitCommandSender(ISendEndpointProvider sendEndpointProvider)
    {
        _sendEndpointProvider = sendEndpointProvider;
    }

    public Task SendAsync(ICommand command, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        destinationEndpointName ??= command.GetType().GetEndpointName();
        return _sendEndpointProvider.SendCommand(command, destinationEndpointName.ToQueueUri(), cancellationToken);
    }
}