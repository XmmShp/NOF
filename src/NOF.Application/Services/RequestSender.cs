namespace NOF;

public interface IRequestSender
{
    Task<Result> SendAsync(IRequest request, string? destinationEndpointName = null, CancellationToken cancellationToken = default);
    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, string? destinationEndpointName = null, CancellationToken cancellationToken = default);
}
