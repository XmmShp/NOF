using MassTransit;
using NOF.Contract;
using NOF.Infrastructure.Core;
using System.Diagnostics;

namespace NOF.Infrastructure.MassTransit;

/// <summary>
/// MassTransit command transport implementation.
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
        IDictionary<string, string?>? headers = null,
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

            var activity = Activity.Current;
            if (activity is not null)
            {
                context.Headers.Set(NOFInfrastructureCoreConstants.Transport.Headers.TraceId, activity.TraceId.ToString());
                context.Headers.Set(NOFInfrastructureCoreConstants.Transport.Headers.SpanId, activity.SpanId.ToString());
            }
        }, cancellationToken);
    }
}
