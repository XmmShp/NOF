namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IJwksService
{
    Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default);
}
