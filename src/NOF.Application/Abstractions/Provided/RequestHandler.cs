using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Non-generic marker interface for request handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestHandler : IMessageHandler;

/// <summary>
/// Handles requests of the specified type without a typed response.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
public interface IRequestHandler<TRequest> : IRequestHandler
    where TRequest : IRequest
{
    /// <summary>Handles the request.</summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handles requests of the specified type with a typed response.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandler<TRequest, TResponse> : IRequestHandler
    where TRequest : class, IRequest<TResponse>
{
    /// <summary>Handles the request and returns a typed response.</summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result containing the response.</returns>
    Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for request handlers (no return value), providing transactional message sending capabilities.
/// Works automatically via AsyncLocal without requiring any injected dependencies.
/// </summary>
public abstract class RequestHandler<TRequest> : HandlerBase, IRequestHandler<TRequest>
    where TRequest : class, IRequest
{
    /// <inheritdoc />
    public abstract Task<Result> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for request handlers (with return value), providing transactional message sending capabilities.
/// Works automatically via AsyncLocal without requiring any injected dependencies.
/// </summary>
public abstract class RequestHandler<TRequest, TResponse> : HandlerBase, IRequestHandler<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    /// <inheritdoc />
    public abstract Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
