using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>
/// Dispatches requests to local handlers within the current process.
/// </summary>
public interface IRequestDispatcher
{
    Task<Result> DispatchAsync(
        object request,
        IDictionary<string, string?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default);

    Task<Result<TResponse>> DispatchAsync<TResponse>(
        object request,
        IDictionary<string, string?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default);
}
