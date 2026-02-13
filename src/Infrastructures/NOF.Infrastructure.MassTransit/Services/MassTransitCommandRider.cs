using MassTransit;
using MassTransit.Mediator;
using NOF.Contract;
using NOF.Infrastructure.Core;

namespace NOF.Infrastructure.MassTransit;

/// <summary>
/// MassTransit command transport implementation.
/// Dispatches locally via mediator when the command type has a local handler
/// and the destination endpoint is null/whitespace or matches the local endpoint.
/// Otherwise dispatches remotely via the bus.
/// </summary>
public class MassTransitCommandRider : ICommandRider
{
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly IScopedMediator _mediator;
    private readonly IEndpointNameProvider _nameProvider;
    private readonly LocalHandlerRegistry _localHandlers;

    public MassTransitCommandRider(
        ISendEndpointProvider sendEndpointProvider,
        IScopedMediator mediator,
        IEndpointNameProvider nameProvider,
        LocalHandlerRegistry localHandlers)
    {
        _sendEndpointProvider = sendEndpointProvider;
        _mediator = mediator;
        _nameProvider = nameProvider;
        _localHandlers = localHandlers;
    }

    public async Task SendAsync(ICommand command,
        IDictionary<string, string?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default)
    {
        if (_localHandlers.ShouldDispatchLocally(command.GetType(), destinationEndpointName))
        {
            await _mediator.Send(command, context => context.ApplyHeaders(headers), cancellationToken);
            return;
        }

        destinationEndpointName ??= _nameProvider.GetEndpointName(command.GetType());
        var sendEndpoint = await _sendEndpointProvider.GetSendEndpoint(destinationEndpointName.ToQueueUri());

        await sendEndpoint.Send(command as object, context => context.ApplyHeaders(headers), cancellationToken);
    }
}
