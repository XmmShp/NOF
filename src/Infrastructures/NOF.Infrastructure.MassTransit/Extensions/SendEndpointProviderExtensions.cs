using MassTransit;

namespace NOF;

// ReSharper disable once InconsistentNaming
public static partial class NOFInfrastructureMassTransitExtensions
{
    ///
    extension(ISendEndpointProvider sendEndpointProvider)
    {
        public async Task SendCommand(ICommand command, Uri destinationAddress, CancellationToken cancellationToken = default)
        {
            var endPoint = await sendEndpointProvider.GetSendEndpoint(destinationAddress);
            await endPoint.Send(command as object, cancellationToken);
        }
    }
}
