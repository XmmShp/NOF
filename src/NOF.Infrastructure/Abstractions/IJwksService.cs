namespace NOF.Infrastructure;

public interface IJwksService
{
    Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default);
}
