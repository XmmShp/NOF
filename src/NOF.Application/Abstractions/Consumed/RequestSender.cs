namespace NOF;

/// <summary>
/// Sends request messages and returns their results.
/// </summary>
public interface IRequestSender
{
    /// <summary>Sends a request without a typed response.</summary>
    /// <param name="request">The request to send.</param>
    /// <param name="destinationEndpointName">Optional destination endpoint name override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<Result> SendAsync(IRequest request, string? destinationEndpointName = null, CancellationToken cancellationToken = default);
    /// <summary>Sends a request and returns a typed response.</summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="destinationEndpointName">Optional destination endpoint name override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result containing the response.</returns>
    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, string? destinationEndpointName = null, CancellationToken cancellationToken = default);
}
