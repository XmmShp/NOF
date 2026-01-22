using MassTransit;

namespace NOF;

/// <summary>
/// MassTransit命令传输实现
/// </summary>
public class MassTransitCommandRider : ICommandRider
{
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly IEndpointNameProvider _nameProvider;

    public MassTransitCommandRider(ISendEndpointProvider sendEndpointProvider, IEndpointNameProvider nameProvider)
    {
        _sendEndpointProvider = sendEndpointProvider;
        _nameProvider = nameProvider;
    }

    public async Task SendAsync(ICommand command,
        IDictionary<string, object?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default)
    {
        destinationEndpointName ??= _nameProvider.GetEndpointName(command.GetType());

        var sendEndpoint = await _sendEndpointProvider.GetSendEndpoint(destinationEndpointName.ToQueueUri());

        await sendEndpoint.Send(command as object, context =>
        {
            if (headers is null)
            {
                return;
            }

            foreach (var header in headers)
            {
                context.Headers.Set(header.Key, header.Value);
            }
        }, cancellationToken);
    }
}
