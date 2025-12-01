using MassTransit;
using MassTransit.Mediator;

namespace NOF;

[ExcludeFromTopology]
[ExcludeFromImplementedTypes]
public interface IRequest : Request<Result>;

[ExcludeFromTopology]
[ExcludeFromImplementedTypes]
public interface IRequest<TResponse> : Request<Result<TResponse>>;