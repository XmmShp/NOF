using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Sends request messages and returns their results.
/// </summary>
public interface IRequestSender
{
    /// <summary>Sends a request with headers and destination.</summary>
    Task<Result> SendAsync(IRequest request, IDictionary<string, string?>? headers, string? destinationEndpointName, CancellationToken cancellationToken = default);

    /// <summary>Sends a request.</summary>
    Task<Result> SendAsync(IRequest request, CancellationToken cancellationToken = default)
        => SendAsync(request, null, null, cancellationToken);

    /// <summary>Sends a request with extra headers.</summary>
    Task<Result> SendAsync(IRequest request, IDictionary<string, string?> headers, CancellationToken cancellationToken = default)
        => SendAsync(request, headers, null, cancellationToken);

    /// <summary>Sends a request to a specific destination.</summary>
    Task<Result> SendAsync(IRequest request, string destinationEndpointName, CancellationToken cancellationToken = default)
        => SendAsync(request, null, destinationEndpointName, cancellationToken);

    /// <summary>Sends a request with headers and destination, returning a typed response.</summary>
    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, IDictionary<string, string?>? headers, string? destinationEndpointName, CancellationToken cancellationToken = default);

    /// <summary>Sends a request, returning a typed response.</summary>
    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        => SendAsync(request, null, null, cancellationToken);

    /// <summary>Sends a request with extra headers, returning a typed response.</summary>
    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, IDictionary<string, string?> headers, CancellationToken cancellationToken = default)
        => SendAsync(request, headers, null, cancellationToken);

    /// <summary>Sends a request to a specific destination, returning a typed response.</summary>
    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, string destinationEndpointName, CancellationToken cancellationToken = default)
        => SendAsync(request, null, destinationEndpointName, cancellationToken);
}
