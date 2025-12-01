using MassTransit;
using MassTransit.Mediator;

namespace NOF;

[ExcludeFromTopology]
public interface IRequestHandler;

[ExcludeFromTopology]
public abstract class RequestHandler<TRequest> : MediatorRequestHandler<RequestWrapper<TRequest>, Result>, IRequestHandler
    where TRequest : class, IRequest
{
    protected sealed override Task<Result> Handle(RequestWrapper<TRequest> request, CancellationToken cancellationToken)
        => HandleAsync(request.Request, cancellationToken);

    public abstract Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

[ExcludeFromTopology]
public abstract class RequestHandler<TRequest, TResponse> : MediatorRequestHandler<RequestWrapper<TRequest, TResponse>, Result<TResponse>>, IRequestHandler
    where TRequest : class, IRequest<TResponse>
{
    protected sealed override Task<Result<TResponse>> Handle(RequestWrapper<TRequest, TResponse> request, CancellationToken cancellationToken)
        => HandleAsync(request.Request, cancellationToken);

    public abstract Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

[ExcludeFromTopology]
public class RequestWrapper<TRequest> : Request<Result> where TRequest : class, IRequest
{
    public TRequest Request { get; }

    internal RequestWrapper(TRequest request)
    {
        Request = request;
    }
}

[ExcludeFromTopology]
public class RequestWrapper<TRequest, TResponse> : Request<Result<TResponse>> where TRequest : class, IRequest<TResponse>
{
    public TRequest Request { get; }

    internal RequestWrapper(TRequest request)
    {
        Request = request;
    }
}