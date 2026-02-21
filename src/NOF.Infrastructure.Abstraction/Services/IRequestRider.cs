using NOF.Contract;

namespace NOF.Infrastructure.Abstraction;

public interface IRequestRider
{
    Task<Result> SendAsync(IRequest request,
        IDictionary<string, string?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default);

    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request,
        IDictionary<string, string?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default);
}
