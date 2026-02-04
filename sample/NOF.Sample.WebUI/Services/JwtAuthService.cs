using NOF;

namespace NOF.Sample.WebUI.Services;

/// <summary>
/// JWT authentication service for demo purposes.
/// </summary>
public class JwtAuthService
{
    private readonly IRequestSender _requestSender;

    public JwtAuthService(IRequestSender requestSender)
    {
        _requestSender = requestSender;
    }

    /// <summary>
    /// Generate JWT token pair for a user.
    /// </summary>
    public async Task<Result<GenerateJwtTokenResponse>> GenerateTokenAsync(
        string userId, 
        string tenantId, 
        string audience = "sample-client",
        string[]? roles = null,
        string[]? permissions = null)
    {
        var request = new GenerateJwtTokenRequest(
            userId,
            tenantId,
            audience,
            TimeSpan.FromMinutes(30), // Access token expires in 30 minutes
            TimeSpan.FromDays(7),     // Refresh token expires in 7 days
            roles,
            permissions
        );

        return await _requestSender.SendAsync(request);
    }

    /// <summary>
    /// Validate refresh token.
    /// </summary>
    public async Task<Result<ValidateRefreshTokenResponse>> ValidateRefreshTokenAsync(string refreshToken)
    {
        var request = new ValidateRefreshTokenRequest(refreshToken);
        return await _requestSender.SendAsync(request);
    }

    /// <summary>
    /// Revoke refresh token.
    /// </summary>
    public async Task<Result> RevokeRefreshTokenAsync(string tokenId, TimeSpan expiration)
    {
        var request = new RevokeRefreshTokenRequest(tokenId, expiration);
        return await _requestSender.SendAsync(request);
    }

    /// <summary>
    /// Get JWKS for token validation.
    /// </summary>
    public async Task<GetJwksResponse?> GetJwksAsync(string audience = "sample-client")
    {
        var request = new GetJwksRequest(audience);
        var response = await _requestSender.SendAsync(request);
        return response.Value;
    }
}
