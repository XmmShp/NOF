using MassTransit.Mediator;

namespace NOF;

public interface IRequest : Request<Result>;

public interface IRequest<TResponse> : Request<Result<TResponse>>
    where TResponse : class;