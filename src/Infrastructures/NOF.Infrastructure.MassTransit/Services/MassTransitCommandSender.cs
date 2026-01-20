using MassTransit;

namespace NOF;

public class MassTransitCommandSender : ICommandSender
{
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly IEndpointNameProvider _nameProvider;

    public MassTransitCommandSender(ISendEndpointProvider sendEndpointProvider, IEndpointNameProvider nameProvider)
    {
        _sendEndpointProvider = sendEndpointProvider;
        _nameProvider = nameProvider;
    }

    public Task SendAsync(ICommand command, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        destinationEndpointName ??= _nameProvider.GetEndpointName(command.GetType());
        return _sendEndpointProvider.SendCommand(command, destinationEndpointName.ToQueueUri(), cancellationToken);
    }
}