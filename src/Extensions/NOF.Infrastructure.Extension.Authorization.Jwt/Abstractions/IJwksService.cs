namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Service for retrieving the authority JWKS document.
/// </summary>
public interface IJwksService
{
    Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default);
}
