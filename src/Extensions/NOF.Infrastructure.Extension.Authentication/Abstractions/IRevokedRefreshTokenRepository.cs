namespace NOF.Infrastructure.Extension.Authentication;

/// <summary>
/// Repository for managing revoked refresh tokens.
/// </summary>
public interface IRevokedRefreshTokenRepository
{
    /// <summary>
    /// Marks a refresh token as revoked.
    /// </summary>
    /// <param name="tokenId">The token identifier (jti).</param>
    /// <param name="expiration">How long the revocation record should be retained.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeAsync(string tokenId, TimeSpan expiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a refresh token has been revoked.
    /// </summary>
    /// <param name="tokenId">The token identifier (jti).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the token has been revoked; otherwise <c>false</c>.</returns>
    Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default);
}
