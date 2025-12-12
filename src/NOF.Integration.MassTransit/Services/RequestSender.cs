using MassTransit.Mediator;

namespace NOF;

public class RequestSender : IRequestSender
{
    private readonly IScopedMediator _mediator;

    public RequestSender(IScopedMediator mediator)
    {
        _mediator = mediator;
    }

    public Task<Result> SendAsync(IRequest request, CancellationToken cancellationToken)
    {
        return _mediator.SendRequest(request, cancellationToken);
    }

    public Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        return _mediator.SendRequest(request, cancellationToken);
    }
}
