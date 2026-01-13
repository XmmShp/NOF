using NOF.Application.Annotations;

namespace NOF;

public interface IRequestHandler<TRequest> : IRequestHandler
    where TRequest : IRequest
{
    Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public interface IRequestHandler<TRequest, TResponse> : IRequestHandler
    where TRequest : class, IRequest<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// RequestHandler 基类（无返回值），提供事务性消息发送能力
/// 无需注入任何依赖，通过 AsyncLocal 自动工作
/// </summary>
public abstract class RequestHandler<TRequest> : HandlerBase, IRequestHandler<TRequest>
    where TRequest : class, IRequest
{
    public abstract Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// RequestHandler 基类（有返回值），提供事务性消息发送能力
/// 无需注入任何依赖，通过 AsyncLocal 自动工作
/// </summary>
public abstract class RequestHandler<TRequest, TResponse> : HandlerBase, IRequestHandler<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    public abstract Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}