using NOF.Contract;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Non-generic marker interface for request handlers. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRequestHandler : IMessageHandler
{
    Task<IResult> HandleAsync(object request, CancellationToken cancellationToken);
}

/// <summary>
/// Handles requests of the specified type without a typed response.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
public interface IRequestHandler<in TRequest> : IRequestHandler
    where TRequest : class
{
    async Task<IResult> IRequestHandler.HandleAsync(object request, CancellationToken cancellationToken)
        => await HandleAsync((TRequest)request, cancellationToken);

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
public interface IRequestHandler<in TRequest, TResponse> : IRequestHandler
    where TRequest : class
{
    async Task<IResult> IRequestHandler.HandleAsync(object request, CancellationToken cancellationToken)
        => await HandleAsync((TRequest)request, cancellationToken);

    /// <summary>Handles the request and returns a typed response.</summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result containing the response.</returns>
    Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
