namespace NOF;

public interface IRequestSender
{
    Task<Result> SendAsync(IRequest request, CancellationToken cancellationToken = default);
    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
