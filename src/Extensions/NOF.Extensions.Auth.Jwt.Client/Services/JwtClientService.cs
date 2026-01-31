using NOF;

namespace NOF;

/// <summary>
/// Client service for JWT token operations using NOF RequestSender.
/// </summary>
public class JwtClientService
{
    private readonly IRequestSender _requestSender;
    private readonly JwtValidationService _validationService;

    public JwtClientService(IRequestSender requestSender, JwtValidationService validationService)
    {
        _requestSender = requestSender;
        _validationService = validationService;
    }

    /// <summary>
    /// Generates a JWT token pair for the specified user.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="roles">The user roles.</param>
    /// <param name="permissions">The token permissions.</param>
    /// <param name="customClaims">Additional custom claims.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated token pair, or null if failed.</returns>
    public async Task<TokenPair?> GenerateTokenAsync(
        string tenantId,
        string userId,
        List<string>? roles = null,
        List<string>? permissions = null,
        Dictionary<string, string>? customClaims = null,
        CancellationToken cancellationToken = default)
    {
        var request = new GenerateJwtTokenRequest(tenantId, userId)
        {
            Roles = roles,
            Permissions = permissions,
            CustomClaims = customClaims
        };

        var result = await _requestSender.SendAsync(request, cancellationToken: cancellationToken);

        if (result.IsFailure)
        {
            // Handle error - could log or throw exception
            return null;
        }

        return result.Value?.TokenPair;
    }

    /// <summary>
    /// Refreshes a JWT token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new token pair, or null if failed.</returns>
    public async Task<TokenPair?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var request = new RefreshJwtTokenRequest(refreshToken);

        var result = await _requestSender.SendAsync(request, cancellationToken: cancellationToken);

        if (result.IsFailure)
        {
            // Handle error
            return null;
        }

        return result.Value?.TokenPair;
    }

    /// <summary>
    /// Validates a JWT token locally.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The token claims if valid, null otherwise.</returns>
    public async Task<JwtClaims?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _validationService.ValidateTokenAsync(token, cancellationToken);
    }

    /// <summary>
    /// Revokes a JWT token.
    /// </summary>
    /// <param name="tokenId">The token ID to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the token was successfully revoked.</returns>
    public async Task<bool> RevokeTokenAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        var request = new RevokeJwtTokenRequest(tokenId);

        var result = await _requestSender.SendAsync(request, cancellationToken: cancellationToken);

        if (result.IsFailure)
        {
            // Handle error
            return false;
        }

        return result.Value?.Revoked ?? false;
    }

    /// <summary>
    /// Revokes all tokens for a specific user.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="userId">The user ID whose tokens should be revoked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the tokens were successfully revoked.</returns>
    public async Task<bool> RevokeUserTokensAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var request = new RevokeUserJwtTokensRequest(userId, tenantId);

        var result = await _requestSender.SendAsync(request, cancellationToken: cancellationToken);

        if (result.IsFailure)
        {
            // Handle error
            return false;
        }

        return result.Value?.Revoked ?? false;
    }

    /// <summary>
    /// Gets the JWKS (JSON Web Key Set) for JWT validation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The JWKS JSON string, or null if failed.</returns>
    public async Task<string?> GetJwksAsync(CancellationToken cancellationToken = default)
    {
        var request = new GetJwksRequest();

        var result = await _requestSender.SendAsync(request, cancellationToken: cancellationToken);

        if (result.IsFailure)
        {
            // Handle error
            return null;
        }

        return result.Value?.JwksJson;
    }

    /// <summary>
    /// Handles token revocation notification.
    /// </summary>
    /// <param name="notification">The revocation notification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task HandleTokenRevokedAsync(TokenRevokedNotification notification, CancellationToken cancellationToken = default)
    {
        await _validationService.HandleTokenRevokedAsync(notification, cancellationToken);
    }
}
