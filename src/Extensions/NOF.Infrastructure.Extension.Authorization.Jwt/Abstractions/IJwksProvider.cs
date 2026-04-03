using Microsoft.IdentityModel.Tokens;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Provides cached signing keys for JWT validation and supports explicit refresh.
/// </summary>
public interface IJwksProvider
{
    /// <summary>
    /// Gets signing keys, loading them on first access if needed.
    /// </summary>
    Task<IReadOnlyList<SecurityKey>> GetSecurityKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a refresh of the cached signing keys.
    /// </summary>
    Task<IReadOnlyList<SecurityKey>> RefreshAsync(CancellationToken cancellationToken = default);
}
