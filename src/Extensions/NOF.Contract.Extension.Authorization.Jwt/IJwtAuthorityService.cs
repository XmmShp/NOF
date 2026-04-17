using System.ComponentModel;

namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Internal service for JWT authority operations.
/// </summary>
public interface IJwtAuthorityService : IRpcService
{
    /// <summary>
    /// Generates a new JWT access token and refresh token pair.
    /// </summary>
    /// <param name="request">The token generation request.</param>
    /// <returns>The generated token pair.</returns>
    [Summary("Issue JWT token pair")]
    [Description("Issues a JWT access token and refresh token for the requested principal.")]
    [Category("JWT Authority")]
    [HttpEndpoint(HttpVerb.Post, "connect/token")]
    Result<GenerateJwtTokenResponse> GenerateJwtToken(GenerateJwtTokenRequest request);

    /// <summary>
    /// Validates a refresh token and returns its claims.
    /// </summary>
    /// <param name="request">The refresh token validation request.</param>
    /// <returns>The validation result with token claims.</returns>
    [Summary("Introspect refresh token")]
    [Description("Validates a refresh token and returns its subject claims if it is still valid.")]
    [Category("JWT Authority")]
    [HttpEndpoint(HttpVerb.Post, "connect/introspect")]
    Result<ValidateJwtRefreshTokenResponse> ValidateJwtRefreshToken(ValidateJwtRefreshTokenRequest request);

    /// <summary>
    /// Revokes a refresh token.
    /// </summary>
    /// <param name="request">The token revocation request.</param>
    /// <returns>The revocation result.</returns>
    [Summary("Revoke refresh token")]
    [Description("Revokes a refresh token so that subsequent validation attempts fail.")]
    [Category("JWT Authority")]
    [HttpEndpoint(HttpVerb.Post, "connect/revocation")]
    Result RevokeJwtRefreshToken(RevokeJwtRefreshTokenRequest request);
}
