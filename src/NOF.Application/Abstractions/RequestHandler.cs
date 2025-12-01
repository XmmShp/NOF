using MassTransit;
using MassTransit.Mediator;

namespace NOF;

[ExcludeFromTopology]
public interface IRequestHandler;

[ExcludeFromTopology]
public abstract class RequestHandler<TRequest> : MediatorRequestHandler<TRequest, Result>, IRequestHandler
    where TRequest : class, IRequest
{
    protected sealed override Task<Result> Handle(TRequest request, CancellationToken cancellationToken)
        => HandleAsync(request, cancellationToken);

    public abstract Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

[ExcludeFromTopology]
public abstract class RequestHandler<TRequest, TResponse> : MediatorRequestHandler<TRequest, Result<TResponse>>, IRequestHandler
    where TRequest : class, IRequest<TResponse>
{
    protected sealed override Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
        => HandleAsync(request, cancellationToken);

    public abstract Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}