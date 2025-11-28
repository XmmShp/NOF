using MassTransit;
using MassTransit.Mediator;

namespace NOF;

[ExcludeFromTopology]
public interface IRequestHandler;

[ExcludeFromTopology]
public abstract class RequestHandler<TCommand> : MediatorRequestHandler<TCommand, Result>, IRequestHandler
    where TCommand : class, IRequest
{
    protected sealed override Task<Result> Handle(TCommand request, CancellationToken cancellationToken)
        => HandleAsync(request, cancellationToken);

    public abstract Task<Result> HandleAsync(TCommand request, CancellationToken cancellationToken);
}

[ExcludeFromTopology]
public abstract class RequestHandler<TCommand, TResponse> : MediatorRequestHandler<TCommand, Result<TResponse>>, IRequestHandler
    where TResponse : class
    where TCommand : class, IRequest<TResponse>
{
    protected sealed override Task<Result<TResponse>> Handle(TCommand request, CancellationToken cancellationToken)
        => HandleAsync(request, cancellationToken);

    public abstract Task<Result<TResponse>> HandleAsync(TCommand request, CancellationToken cancellationToken);
}
